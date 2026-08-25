using System;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim.VisualStudio.UnitTest.Mock;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using DteCommand = EnvDTE.Command;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class ExtensionsTest
    {
        private readonly MockRepository _factory;

        public ExtensionsTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
        }

        public sealed class KeyBindingTest
        {
            /// <summary>
            /// Bindings as an array
            /// </summary>
            [Fact]
            public void GetKeyBindings1()
            {
                var com = MockObjectFactory2.CreateCommand(0, "name", "::f");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Single(list);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("name", list[0].Name);
            }

            [Fact]
            public void GetKeyBindings2()
            {
                var com = MockObjectFactory2.CreateCommand(0, "name", "foo::f", "bar::b");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("foo", list[0].KeyBinding.Scope);
                Assert.Equal('b', list[1].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal("bar", list[1].KeyBinding.Scope);
            }

            /// <summary>
            /// Bindings as a string which is what the documentation indicates it should be
            /// </summary>
            [Fact]
            public void GetKeyBindings3()
            {
                var com = MockObjectFactory2.CreateCommand(0, "name", "::f");
                var list = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Single(list);
                Assert.Equal('f', list[0].KeyBinding.FirstKeyStroke.KeyInput.Char);
                Assert.Equal(string.Empty, list[0].KeyBinding.Scope);
            }

            /// <summary>
            /// A bad key binding should just return as an empty result set
            /// </summary>
            [Fact]
            public void GetKeyBindings4()
            {
                var com = MockObjectFactory2.CreateCommand(0, "name", "::notavalidkey");
                var e = Extensions.GetCommandKeyBindings(com.Object).ToList();
                Assert.Empty(e);
            }
        }

        public sealed class CommandTest : ExtensionsTest
        {
            /// <summary>
            /// Make sure that we can handle the case where the Bindings call throws
            /// </summary>
            [Fact]
            public void GetBindingsThrows()
            {
                var mock = _factory.Create<DteCommand>();
                mock.SetupGet(x => x.Bindings).Throws(new OutOfMemoryException());
                var all = mock.Object.GetBindings();
                Assert.Empty(all);
            }

            [Fact]
            public void SafeResetKeyBindings()
            {
                var mock = new Mock<DteCommand>(MockBehavior.Strict);
                mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Verifiable();
                mock.Object.SafeResetBindings();
                mock.Verify();
            }

            /// <summary>
            /// Some Command implementations return E_FAIL, just ignore it
            /// </summary>
            [Fact]
            public void SafeResetKeyBindings2()
            {
                var mock = new Mock<DteCommand>(MockBehavior.Strict);
                mock.SetupSet(x => x.Bindings = It.IsAny<object>()).Throws(new COMException()).Verifiable();
                mock.Object.SafeResetBindings();
                mock.Verify();
            }
        }

        public sealed class VsCodeWindowTest : ExtensionsTest
        {
            [Fact]
            public void IsSplit1()
            {
                var codeWindow = _factory.Create<IVsCodeWindow>();
                var adapter = _factory.Create<IVsAdapter>();
                adapter.SetupGet(x => x.EditorAdapter).Returns(_factory.Create<IVsEditorAdaptersFactoryService>().Object);
                codeWindow.MakeSplit(adapter, factory: _factory);
                Assert.True(codeWindow.Object.IsSplit());
                _factory.Verify();
            }
        }

        public sealed class GetAdornmentLayerNoThrowTest : ExtensionsTest
        {
            private static readonly object s_layerKey = new object();
            private static readonly string s_layerName = "MyAdornmentLayer";
            private readonly Mock<IWpfTextView> _wpfTextView;
            private readonly PropertyCollection _propertyCollection;

            public GetAdornmentLayerNoThrowTest()
            {
                _propertyCollection = new PropertyCollection();
                _wpfTextView = _factory.Create<IWpfTextView>();
                _wpfTextView.SetupGet(x => x.Properties).Returns(_propertyCollection);
            }

            [Fact]
            public void FirstTimeLayerNotPresent()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new ArgumentOutOfRangeException());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
            }

            /// <summary>
            /// The second time around using the same name and key shouldn't call the GetLayer 
            /// method.  No need to keep throwing exceptions and catching them.  It just needlessly
            /// affects perf and kills the debugging experience 
            /// </summary>
            [Fact]
            public void SecondTimeLayerNotPresent()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new ArgumentOutOfRangeException());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
                var calledAgain = false;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Callback(() => { calledAgain = true; }).Throws(new ArgumentOutOfRangeException());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
                Assert.False(calledAgain);
            }

            [Fact]
            public void HasTheLayer()
            {
                var layer = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
            }

            /// <summary>
            /// A layer that was found is cached the same way a missing one is.  Every call after the first
            /// should be a lookup on the view instead of a trip back through the editor, because the editor
            /// call creates the layer as a side effect and that isn't always safe to do
            /// </summary>
            [Fact]
            public void SecondTimeLayerPresent()
            {
                var layer = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
                var calledAgain = false;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Callback(() => { calledAgain = true; }).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
                Assert.False(calledAgain);
            }

            /// <summary>
            /// Only ArgumentOutOfRangeException means "this view has no layer by that name".  Any other
            /// exception is the editor failing to answer, and caching that would disable the lookup for
            /// the life of the view
            /// </summary>
            [Fact]
            public void UnexpectedFailureIsNotCached()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new InvalidOperationException());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));

                var layer = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
            }

            /// <summary>
            /// Same rule for an exception type we don't recognize at all
            /// </summary>
            [Fact]
            public void UnknownFailureIsNotCached()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new Exception());
                Assert.Null(_wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));

                var layer = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Returns(layer);
                Assert.Same(layer, _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey));
            }
        }

        public sealed class TryGetCachedAdornmentLayerTest : ExtensionsTest
        {
            private static readonly object s_layerKey = new object();
            private static readonly string s_layerName = "MyAdornmentLayer";
            private readonly Mock<IWpfTextView> _wpfTextView;
            private readonly PropertyCollection _propertyCollection;

            public TryGetCachedAdornmentLayerTest()
            {
                _propertyCollection = new PropertyCollection();
                _wpfTextView = _factory.Create<IWpfTextView>();
                _wpfTextView.SetupGet(x => x.Properties).Returns(_propertyCollection);
            }

            /// <summary>
            /// Nothing has resolved the layer yet so there is no answer to give.  Importantly this must not
            /// fall back to asking the editor, because that would create the layer
            /// </summary>
            [Fact]
            public void NotPrimed()
            {
                Assert.False(_wpfTextView.Object.TryGetCachedAdornmentLayer(s_layerName, s_layerKey, out IAdornmentLayer layer));
                Assert.Null(layer);
                _wpfTextView.Verify(x => x.GetAdornmentLayer(It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public void PrimedWithLayer()
            {
                var expected = _factory.Create<IAdornmentLayer>().Object;
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Returns(expected);
                _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey);

                Assert.True(_wpfTextView.Object.TryGetCachedAdornmentLayer(s_layerName, s_layerKey, out IAdornmentLayer layer));
                Assert.Same(expected, layer);
            }

            /// <summary>
            /// The layer isn't defined for this view.  That is a real answer, not a cache miss
            /// </summary>
            [Fact]
            public void PrimedWithoutLayer()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new ArgumentOutOfRangeException());
                _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey);

                Assert.True(_wpfTextView.Object.TryGetCachedAdornmentLayer(s_layerName, s_layerKey, out IAdornmentLayer layer));
                Assert.Null(layer);
            }

            [Fact]
            public void PrimingFailedTransiently()
            {
                _wpfTextView.Setup(x => x.GetAdornmentLayer(s_layerName)).Throws(new InvalidOperationException());
                _wpfTextView.Object.GetAdornmentLayerNoThrow(s_layerName, s_layerKey);

                Assert.False(_wpfTextView.Object.TryGetCachedAdornmentLayer(s_layerName, s_layerKey, out IAdornmentLayer layer));
                Assert.Null(layer);
            }
        }
    }
}
