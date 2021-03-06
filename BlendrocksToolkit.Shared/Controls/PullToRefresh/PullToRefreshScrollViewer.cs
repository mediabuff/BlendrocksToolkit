﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace BlendrocksToolkit.Controls
{
    public sealed class PullToRefreshScrollViewer : ContentControl
    {
        private const string ScrollViewerControl = "ScrollViewer";
        private const string ContainerGrid = "ContainerGrid";
        private const string PullToRefreshIndicator = "PullToRefreshIndicator";
        private const string VisualStateNormal = "Normal";
        private const string VisualStateReadyToRefresh = "ReadyToRefresh";

        private DispatcherTimer compressionTimer;
        private ScrollViewer scrollViewer;
        private DispatcherTimer timer;
        private Grid containerGrid;
        private Border pullToRefreshIndicator;
        private bool isCompressionTimerRunning;
        private bool isReadyToRefresh;
        private bool isCompressedEnough;

        public event EventHandler RefreshContent;

        public static readonly DependencyProperty PullTextProperty = DependencyProperty.Register("PullText", typeof(string), typeof(PullToRefreshScrollViewer), new PropertyMetadata("Pull to refresh"));
        public static readonly DependencyProperty RefreshTextProperty = DependencyProperty.Register("RefreshText", typeof(string), typeof(PullToRefreshScrollViewer), new PropertyMetadata("Release to refresh"));
        public static readonly DependencyProperty RefreshHeaderHeightProperty = DependencyProperty.Register("RefreshHeaderHeight", typeof(double), typeof(PullToRefreshScrollViewer), new PropertyMetadata(100D));
        public static readonly DependencyProperty RefreshCommandProperty = DependencyProperty.Register("RefreshCommand", typeof(ICommand), typeof(PullToRefreshScrollViewer), new PropertyMetadata(default(ICommand)));
        public static readonly DependencyProperty ArrowColorProperty = DependencyProperty.Register("ArrowColor", typeof(Brush), typeof(PullToRefreshScrollViewer), new PropertyMetadata(new SolidColorBrush(Colors.Red)));
#if WINDOWS_PHONE_APP
        private double offsetTreshhold = 100;
#endif
#if WINDOWS_APP
        private double offsetTreshhold = 70;
#endif
        public PullToRefreshScrollViewer()
        {
            DefaultStyleKey = typeof(PullToRefreshScrollViewer);
            Loaded += PullToRefreshScrollViewer_Loaded;
        }
        public ICommand RefreshCommand
        {
            get { return (ICommand)GetValue(RefreshCommandProperty); }
            set { SetValue(RefreshCommandProperty, value); }
        }
        public double RefreshHeaderHeight
        {
            get { return (double)GetValue(RefreshHeaderHeightProperty); }
            set { SetValue(RefreshHeaderHeightProperty, value); }
        }
        public string RefreshText
        {
            get { return (string)GetValue(RefreshTextProperty); }
            set { SetValue(RefreshTextProperty, value); }
        }
        public string PullText
        {
            get { return (string)GetValue(PullTextProperty); }
            set { SetValue(PullTextProperty, value); }
        }
        public Brush ArrowColor
        {
            get { return (Brush)GetValue(ArrowColorProperty); }
            set { SetValue(ArrowColorProperty, value); }
        }
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            scrollViewer = (ScrollViewer)GetTemplateChild(ScrollViewerControl);
            scrollViewer.ViewChanging += ScrollViewer_ViewChanging;
            scrollViewer.Margin = new Thickness(0, 0, 0, -RefreshHeaderHeight);
            var transform = new CompositeTransform();
            transform.TranslateY = -RefreshHeaderHeight;
            scrollViewer.RenderTransform = transform;
            containerGrid = (Grid)GetTemplateChild(ContainerGrid);
            pullToRefreshIndicator = (Border)GetTemplateChild(PullToRefreshIndicator);
            SizeChanged += OnSizeChanged;
        }
        /// <summary>
        /// Initiate timers to detect if we're scrolling into negative space
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PullToRefreshScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            compressionTimer = new DispatcherTimer();
            compressionTimer.Interval = TimeSpan.FromSeconds(1);
            compressionTimer.Tick += CompressionTimer_Tick;
            timer.Start();
        }
        /// <summary>
        /// Clip the bounds of the control to avoid showing the pull to refresh text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Clip = new RectangleGeometry()
            {
                Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
            };
        }
        /// <summary>
        /// Detect if we've scrolled all the way to the top. Stop timers when we're not completely in the top
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (e.NextView.VerticalOffset == 0)
            {
                timer.Start();
            }
            else
            {
                if (timer != null)
                {
                    timer.Stop();
                }
                if (compressionTimer != null)
                {
                    compressionTimer.Stop();
                }
                isCompressionTimerRunning = false;
                isCompressedEnough = false;
                isReadyToRefresh = false;
                VisualStateManager.GoToState(this, VisualStateNormal, true);
            }
        }
        /// <summary>
        /// Detect if I've scrolled far enough and been there for enough time to refresh
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CompressionTimer_Tick(object sender, object e)
        {
            if (isCompressedEnough)
            {
                VisualStateManager.GoToState(this, VisualStateReadyToRefresh, true);
                isReadyToRefresh = true;
            }
            else
            {
                isCompressedEnough = false;
                compressionTimer.Stop();
            }
        }
        /// <summary>
        /// Invoke timer if we've scrolled far enough up into negative space. If we get back to offset 0 the refresh command and event is invoked. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, object e)
        {
            if (containerGrid != null)
            {
                Rect elementBounds = pullToRefreshIndicator.TransformToVisual(containerGrid).TransformBounds(new Rect(0.0, 0.0, pullToRefreshIndicator.Height, RefreshHeaderHeight));
                var compressionOffset = elementBounds.Bottom;
                Debug.WriteLine(compressionOffset);
                if (compressionOffset > offsetTreshhold)
                {
                    if (isCompressionTimerRunning == false)
                    {
                        isCompressionTimerRunning = true;
                        compressionTimer.Start();
                    }
                    isCompressedEnough = true;
                }
                else if (compressionOffset == 0 && isReadyToRefresh == true)
                {
                    InvokeRefresh();
                }
                else
                {
                    isCompressedEnough = false;
                    isCompressionTimerRunning = false;
                }
            }
        }
        /// <summary>
        /// Set correct visual state and invoke refresh event and command
        /// </summary>
        private void InvokeRefresh()
        {
            isReadyToRefresh = false;
            VisualStateManager.GoToState(this, VisualStateNormal, true);
            if (RefreshContent != null)
            {
                RefreshContent(this, EventArgs.Empty);
            }
            if (RefreshCommand != null && RefreshCommand.CanExecute(null) == true)
            {
                RefreshCommand.Execute(null);
            }
        }
    }
}
