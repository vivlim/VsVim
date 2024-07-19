using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Ink;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text.Formatting;

namespace VimCanvasApp.Implementation.NewLineDisplay
{
    /// <summary>
    /// Adds adornments to make new lines visible in the editor
    /// </summary>
    internal sealed class CanvasAdornment
    {
        private static readonly ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> EmptyTagColllection = new ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>>(new List<ITagSpan<IntraTextAdornmentTag>>());
        private readonly IWpfTextView _wpfTextView;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly IVimAppOptions _vimAppOptions;

        private readonly object adornmentTag = new object();

        private InkCanvas? _inkCanvas;
        private StrokeCollection _strokeCollection = new();
        private LineMappedStrokeCollection _lineMappedStrokeCollection = new();


        internal CanvasAdornment(IWpfTextView textView, IAdornmentLayer adornmentLayer, IVimAppOptions vimAppOptions)
        {
            _wpfTextView = textView;
            _adornmentLayer = adornmentLayer;
            _vimAppOptions = vimAppOptions;

            _wpfTextView.Closed += OnClosed;
            _wpfTextView.LayoutChanged += OnLayoutChanged;
            _wpfTextView.TextBuffer.Changed += TextBuffer_Changed;
            _vimAppOptions.Changed += OnOptionsChanged;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _wpfTextView.Closed -= OnClosed;
            _wpfTextView.LayoutChanged -= OnLayoutChanged;
            _wpfTextView.TextBuffer.Changed -= TextBuffer_Changed;
            _vimAppOptions.Changed -= OnOptionsChanged;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            CreateVisuals();
        }

        private void OnOptionsChanged(object sender, EventArgs e)
        {
            CreateVisuals();
        }

        private void TextBuffer_Changed(object? sender, TextContentChangedEventArgs e)
        {
            CreateVisuals();
        }

        private UIElement CreateAdornment(string text)
        {
            var inkCanvas = new InkCanvas
            {
                
            };
            //inkCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            inkCanvas.Measure(new Size(69, 69));
            return inkCanvas;
        }

        private void CreateVisuals()
        {
            //try
            //{
            if (this._inkCanvas is InkCanvas existing)
            {
                // _adornmentLayer.RemoveAdornmentsByTag(adornmentTag);
                //_adornmentLayer.RemoveAllAdornments();
                //existing.StrokeCollected -= Canvas_StrokeCollected;
                //existing.Background = Brushes.Red;
                //this._inkCanvas = null;
                this._inkCanvas.Strokes = new StrokeCollection(this._lineMappedStrokeCollection.GetStrokes(this._wpfTextView));
            }
            else
            {
                var inkCanvasAdornment = this.CreateCanvas();
                inkCanvasAdornment.Background = new SolidColorBrush(Color.FromArgb(32, 128, 128, 128));
                inkCanvasAdornment.Width = _adornmentLayer.TextView.ViewportWidth;
                inkCanvasAdornment.Height = _adornmentLayer.TextView.ViewportHeight;

                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, adornmentTag, inkCanvasAdornment, null);
                this._inkCanvas = inkCanvasAdornment;
            }
                
                //var firstLine = _adornmentLayer.TextView.GetFirstLine();
                //var lastLine = _adornmentLayer.TextView.GetLastLine();

                //var firstGeo = _wpfTextView.TextViewLines.GetMarkerGeometry(firstLine,)

                //Geometry geometry = _adornmentLayer.TextView.height
                
                //CreateVisuals(_wpfTextView.TextViewLines);
            //}
            //catch (Exception)
            //{

            //}
        }

        private InkCanvas CreateCanvas()
        {
            var canvas = new InkCanvas();
            canvas.Strokes = new StrokeCollection(this._lineMappedStrokeCollection.GetStrokes(this._wpfTextView));

            //canvas.StrokeCollected += (sender, e) =>
            //{
            //    this._lineMappedStrokeCollection.AddStroke(e.Stroke, this._wpfTextView);
            //};
            canvas.StrokeCollected += Canvas_StrokeCollected;

            return canvas;
        }

        private void Canvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            this._lineMappedStrokeCollection.AddStroke(e.Stroke, this._wpfTextView);
        }

        private class LineMappedStrokeCollection
        {
            private readonly List<LineStroke> lineStrokes = new();

            public LineMappedStrokeCollection()
            {
            }

            public void AddStroke(Stroke stroke, IWpfTextView textView)
            {
                var bounds = stroke.GetBounds();
                var topLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(bounds.Top);
                var bottomLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(bounds.Bottom);
                double? linePixelDistance = null;

                if (topLine != null)
                {
                    var translate = new TranslateTransform(0.0, -topLine.TextTop);
                    stroke = stroke.Clone();
                    stroke.Transform(translate.Value, applyToStylusTip: false);

                    if (bottomLine != null)
                    {
                        linePixelDistance = bottomLine.TextTop - topLine.TextTop;
                    }
                }


                this.lineStrokes.Add(new(topLine?.Start, bottomLine?.End, linePixelDistance, stroke));
            }


            public IReadOnlyList<Stroke> GetStrokes(IWpfTextView textView)
            {
                List<LineStroke> strokesToRemove = new();
                var translatedStrokes =  this.lineStrokes.Select(s =>
                {
                    var stroke = s.stroke.Clone();
                    if (s.top is SnapshotPoint origTop)
                    {
                        var newTop = origTop.TranslateTo(textView.TextSnapshot, PointTrackingMode.Negative);
                        if (textView.TryGetTextViewLineContainingBufferPosition(newTop, out var topLine))
                        {
                            var translate = new TranslateTransform(0.0, topLine.TextTop);
                            stroke.Transform(translate.Value, applyToStylusTip: false);

                            if (s.bottom is SnapshotPoint origBottom && s.linePixelDistance is double origLinePixelDistance)
                            {
                                var newBottom = origBottom.TranslateTo(textView.TextSnapshot, PointTrackingMode.Negative);
                                if (textView.TryGetTextViewLineContainingBufferPosition(newBottom, out var bottomLine))
                                {
                                    var newLinePixelDistance = bottomLine.TextTop - topLine.TextTop;

                                    if (newLinePixelDistance == 0.0)
                                    {
                                        strokesToRemove.Add(s);
                                    }
                                    else
                                    {
                                        var verticalScaleFactor = newLinePixelDistance / origLinePixelDistance;
                                        var scale = new ScaleTransform(1.0, verticalScaleFactor);
                                        stroke.Transform(scale.Value, applyToStylusTip: false);
                                    }
                                }
                            }
                        }
                    }

                    return stroke;
                }).ToArray();

                foreach (var strokeToRemove in strokesToRemove)
                {
                    this.lineStrokes.Remove(strokeToRemove);
                }
                return translatedStrokes;
            }

            private record LineStroke(SnapshotPoint? top, SnapshotPoint? bottom, double? linePixelDistance, Stroke stroke);
        }
    }
}
