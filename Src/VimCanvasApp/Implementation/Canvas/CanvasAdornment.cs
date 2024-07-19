﻿using Vim.EditorHost;
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
            private readonly HashSet<LineStroke> lineStrokes = new();

            public LineMappedStrokeCollection()
            {
            }

            public void AddStroke(Stroke stroke, IWpfTextView textView)
            {
                var bounds = stroke.GetBounds();
                var topLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(bounds.Top);
                var bottomLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(bounds.Bottom);
                double? linePixelDistance = null;

                if (topLine != null && bottomLine != null)
                {
                    linePixelDistance = bottomLine.TextBottom - topLine.TextTop;
                }

                this.lineStrokes.Add(new LineStroke {
                    Top = topLine?.Start,
                    Bottom = bottomLine?.End,
                    LastTopY = topLine?.TextTop,
                    LastBottomY = bottomLine?.TextBottom,
                    Stroke = stroke
                });
            }


            public ICollection<Stroke> GetStrokes(IWpfTextView textView)
            {
                List<LineStroke> strokesToRemove = new();
                foreach (var s in this.lineStrokes)
                {
                    if (s.Top is SnapshotPoint origTop && s.LastTopY is double lastTopY)
                    {
                        var newTop = origTop.TranslateTo(textView.TextSnapshot, PointTrackingMode.Negative);
                        if (textView.TryGetTextViewLineContainingBufferPosition(newTop, out var topLine))
                        {
                            if (s.Bottom is SnapshotPoint origBottom && s.LastBottomY is double lastBottomY)
                            {
                                var newBottom = origBottom.TranslateTo(textView.TextSnapshot, PointTrackingMode.Negative);
                                if (textView.TryGetTextViewLineContainingBufferPosition(newBottom, out var bottomLine))
                                {
                                    var newHeight = bottomLine.TextBottom - topLine.TextTop;
                                    var lastHeight = lastBottomY - lastTopY;

                                    if (newHeight <= 0.0)
                                    {
                                        strokesToRemove.Add(s);
                                    }
                                    else
                                    {
                                        var verticalScaleFactor = newHeight / lastHeight;
                                        var scale = new ScaleTransform(1.0, verticalScaleFactor);
                                        s.Stroke.Transform(scale.Value, applyToStylusTip: false);
                                    }


                                    s.Bottom = newBottom;
                                    s.LastBottomY = bottomLine.TextBottom;
                                }
                                else
                                {
                                    s.Bottom = null;
                                    s.LastBottomY = null;
                                }
                            }

                            var yDelta = topLine.TextTop - lastTopY;
                            if (yDelta != 0.0)
                            {
                                var translate = new TranslateTransform(0.0, yDelta);
                                s.Stroke.Transform(translate.Value, applyToStylusTip: false);
                                s.LastTopY = topLine.TextTop;
                            }

                            s.Top = newTop;
                        }
                        else
                        {
                            strokesToRemove.Add(s);
                        }
                    }
                }

                foreach (var strokeToRemove in strokesToRemove)
                {
                    this.lineStrokes.Remove(strokeToRemove);
                }

                return this.lineStrokes.Select(s => s.Stroke).ToList();
            }

            private class LineStroke
            {
                public SnapshotPoint? Top { get; set; }
                public SnapshotPoint? Bottom { get; set; }

                public double? LastTopY { get; set; }
                public double? LastBottomY { get; set; }
                public required Stroke Stroke { get; init; }
            }
        }
    }
}
