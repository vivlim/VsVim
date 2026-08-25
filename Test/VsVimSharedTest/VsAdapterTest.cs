using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Moq;
using System.Collections.Generic;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.Misc;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public class VsAdapterTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly Mock<IVsEditorAdaptersFactoryService> _editorAdapterFactory;
        private readonly Mock<IEditorOptionsFactoryService> _editorOptionsFactory;
        private readonly Mock<IIncrementalSearchFactoryService> _incrementalSearchFactoryService;
        private readonly Mock<_DTE> _dte;
        private readonly Mock<IExtensionAdapterBroker> _extensionAdapterBroker;
        private readonly Mock<SVsServiceProvider> _serviceProvider;
        internal readonly VsAdapter _adapterRaw;
        private readonly IVsAdapter _adapter;

        public VsAdapterTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _editorAdapterFactory = _factory.Create<IVsEditorAdaptersFactoryService>();
            _editorOptionsFactory = _factory.Create<IEditorOptionsFactoryService>();
            _incrementalSearchFactoryService = _factory.Create<IIncrementalSearchFactoryService>();
            _extensionAdapterBroker = _factory.Create<IExtensionAdapterBroker>();
            _serviceProvider = _factory.Create<SVsServiceProvider>();
            _serviceProvider.MakeService<SVsTextManager, IVsTextManager>(_factory);
            _serviceProvider.MakeService<SVsUIShell, IVsUIShell>(_factory);
            _serviceProvider.MakeService<SVsRunningDocumentTable, IVsRunningDocumentTable>(_factory);
            _dte = _serviceProvider.MakeService<SDTE, _DTE>(_factory);
            _dte.SetupGet(x => x.Version).Returns("10.0");
            _adapterRaw = new VsAdapter(
                _editorAdapterFactory.Object,
                _editorOptionsFactory.Object,
                _incrementalSearchFactoryService.Object,
                _extensionAdapterBroker.Object,
                _serviceProvider.Object);
            _adapter = _adapterRaw;
        }

        public sealed class IsReadOnlyTest : VsAdapterTest
        {
            private readonly ITextView _textView;
            private readonly Mock<IVsTextBuffer> _vsTextBuffer;

            public IsReadOnlyTest()
            {
                _textView = CreateTextView();
                _vsTextBuffer = _editorAdapterFactory.MakeBufferAdapter(_textView.TextBuffer, _factory);
            }

            [WpfFact]
            public void IsSetViewProhibitUserInput()
            {
                _textView.Options.SetOptionValue(DefaultTextViewOptions.ViewProhibitUserInputId, true);
                Assert.True(_adapter.IsReadOnly(_textView));
                _factory.Verify();
            }

            [WpfFact]
            public void IsNotSetViewProhibitUserInput()
            {
                _textView.Options.SetOptionValue(DefaultTextViewOptions.ViewProhibitUserInputId, false);
                Assert.False(_adapter.IsReadOnly(_textView));
                _factory.Verify();
            }

            [WpfFact]
            public void BufferReadOnlyCheckFails()
            {
                uint flags;
                _vsTextBuffer
                    .Setup(x => x.GetStateFlags(out flags))
                    .Returns(VSConstants.E_FAIL);
                Assert.False(_adapter.IsReadOnly(_textView.TextBuffer));
                Assert.False(_adapter.IsReadOnly(_textView));
                _factory.Verify();
            }

            [WpfFact]
            public void BufferIsntReadOnly()
            {
                var flags = 0u;
                _vsTextBuffer
                    .Setup(x => x.GetStateFlags(out flags))
                    .Returns(VSConstants.S_OK);
                Assert.False(_adapter.IsReadOnly(_textView.TextBuffer));
                Assert.False(_adapter.IsReadOnly(_textView));
                _factory.Verify();
            }

            [WpfFact]
            public void BufferIsReadOnly()
            {
                var flags = (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
                _vsTextBuffer
                    .Setup(x => x.GetStateFlags(out flags))
                    .Returns(VSConstants.S_OK);
                Assert.True(_adapter.IsReadOnly(_textView.TextBuffer));
                Assert.True(_adapter.IsReadOnly(_textView));
                _factory.Verify();
            }
        }

        public sealed class IsIncrementalSearchActive : VsAdapterTest
        {
            /// <summary>
            /// Test the case where the ITextView doesn't have the FindUILayer adornment layer and hence
            /// it's possible that the query will fail 
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                var textView = CreateTextView();
                Assert.False(_adapterRaw.IsIncrementalSearchActive(textView));
            }

            [WpfFact]
            public void ExtensionBroker()
            {
                var textView = CreateTextView();
                _extensionAdapterBroker.Setup(x => x.IsIncrementalSearchActive(textView)).Returns(true);
                Assert.True(_adapterRaw.IsIncrementalSearchActive(textView));
            }

            [WpfFact]
            public void ExtensionBrokerFalse()
            {
                var textView = CreateTextView();
                _extensionAdapterBroker.Setup(x => x.IsIncrementalSearchActive(textView)).Returns(false);
                Assert.False(_adapterRaw.IsIncrementalSearchActive(textView));
            }

            /// <summary>
            /// This runs from IOleCommandTarget.QueryStatus, which Visual Studio raises from inside a WPF
            /// layout pass.  Asking the editor for an adornment layer there creates it, which mutates the
            /// visual tree WPF is walking and crashes the process.  The screen scrape must only ever read a
            /// layer that PrimeFindAdornmentLayer already resolved
            /// </summary>
            [WpfFact]
            public void ScreenScrapeNeverCreatesTheLayer()
            {
                var wpfTextView = new Mock<IWpfTextView>(MockBehavior.Strict);
                wpfTextView.SetupGet(x => x.Properties).Returns(new PropertyCollection());

                Assert.False(_adapterRaw.IsIncrementalSearchActiveScreenScrape(wpfTextView.Object));
                wpfTextView.Verify(x => x.GetAdornmentLayer(It.IsAny<string>()), Times.Never());
            }

            /// <summary>
            /// Priming is what makes the read-only screen scrape above able to see anything at all
            /// </summary>
            [WpfFact]
            public void PrimingResolvesTheLayer()
            {
                var layer = _factory.Create<IAdornmentLayer>();
                layer.SetupGet(x => x.Elements).Returns(new List<IAdornmentLayerElement>().AsReadOnly());

                var wpfTextView = new Mock<IWpfTextView>(MockBehavior.Strict);
                wpfTextView.SetupGet(x => x.Properties).Returns(new PropertyCollection());
                wpfTextView.SetupGet(x => x.IsClosed).Returns(false);
                wpfTextView.SetupGet(x => x.InLayout).Returns(false);
                wpfTextView.Setup(x => x.GetAdornmentLayer(VsAdapter.FindUIAdornmentLayerName)).Returns(layer.Object);

                _adapterRaw.PrimeFindAdornmentLayer(wpfTextView.Object);
                wpfTextView.Verify(x => x.GetAdornmentLayer(VsAdapter.FindUIAdornmentLayerName), Times.Once());

                // The layer is now cached, so the scrape can read it without going back to the editor
                Assert.False(_adapterRaw.IsIncrementalSearchActiveScreenScrape(wpfTextView.Object));
                wpfTextView.Verify(x => x.GetAdornmentLayer(VsAdapter.FindUIAdornmentLayerName), Times.Once());
            }

            /// <summary>
            /// Creating the layer mutates the view's visual tree.  Doing that while WPF is walking that
            /// tree is the crash this whole mechanism exists to avoid, so priming has to decline rather
            /// than trust that every caller checked
            /// </summary>
            [WpfFact]
            public void PrimingDeclinesDuringLayout()
            {
                var wpfTextView = new Mock<IWpfTextView>(MockBehavior.Strict);
                wpfTextView.SetupGet(x => x.IsClosed).Returns(false);
                wpfTextView.SetupGet(x => x.InLayout).Returns(true);

                _adapterRaw.PrimeFindAdornmentLayer(wpfTextView.Object);
                wpfTextView.Verify(x => x.GetAdornmentLayer(It.IsAny<string>()), Times.Never());
            }

            /// <summary>
            /// A view that was skipped during layout has to be primeable later, otherwise it reports the
            /// find UI as inactive forever
            /// </summary>
            [WpfFact]
            public void PrimingRetriesAfterLayout()
            {
                var layer = _factory.Create<IAdornmentLayer>();
                layer.SetupGet(x => x.Elements).Returns(new List<IAdornmentLayerElement>().AsReadOnly());

                var inLayout = true;
                var wpfTextView = new Mock<IWpfTextView>(MockBehavior.Strict);
                wpfTextView.SetupGet(x => x.Properties).Returns(new PropertyCollection());
                wpfTextView.SetupGet(x => x.IsClosed).Returns(false);
                wpfTextView.SetupGet(x => x.InLayout).Returns(() => inLayout);
                wpfTextView.Setup(x => x.GetAdornmentLayer(VsAdapter.FindUIAdornmentLayerName)).Returns(layer.Object);

                _adapterRaw.PrimeFindAdornmentLayer(wpfTextView.Object);
                wpfTextView.Verify(x => x.GetAdornmentLayer(It.IsAny<string>()), Times.Never());

                inLayout = false;
                _adapterRaw.PrimeFindAdornmentLayer(wpfTextView.Object);
                wpfTextView.Verify(x => x.GetAdornmentLayer(VsAdapter.FindUIAdornmentLayerName), Times.Once());
            }
        }

        public sealed class MiscTest : VsAdapterTest
        {
            /// <summary>
            /// The power tools quick find is considered an incremental search 
            /// </summary>
            [WpfFact]
            public void IsIncrementalSearchActive_Extension()
            {
                var textView = _factory.Create<ITextView>().Object;
                _extensionAdapterBroker.Setup(x => x.IsIncrementalSearchActive(textView)).Returns(true);
                Assert.True(_adapter.IsIncrementalSearchActive(textView));
            }
        }
    }
}
