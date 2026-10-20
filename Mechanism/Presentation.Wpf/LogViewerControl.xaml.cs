using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CxVisual.Foundation.Infrastructure.Logging;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>只呈现公共日志的WPF控件，不拥有日志文件或工程对象。</summary>
    public partial class LogViewerControl : UserControl
    {
        private readonly ObservableCollection<FLDLogRecord> _items
            = new ObservableCollection<FLDLogRecord>();
        private FLDLogService _hub;
        private ICollectionView _view;
        private const int VisibleLimit = 2000;

        public LogViewerControl()
        {
            InitializeComponent();
            LogTable.ItemsSource = _items;
            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = Accept;
            Unloaded += (sender, args) => Detach();
        }

        public void Attach(FLDLogService hub)
        {
            if (hub == null) throw new ArgumentNullException("hub");
            if (!Dispatcher.CheckAccess())
                throw new InvalidOperationException("Attach must run on the WPF UI thread.");
            Detach();
            _hub = hub;
            _items.Clear();
            foreach (FLDLogRecord item in hub.Snapshot()) _items.Add(item);
            _hub.RecordReceived += Received;
            _view.Refresh();
        }

        public void Detach()
        {
            if (_hub != null) _hub.RecordReceived -= Received;
            _hub = null;
        }

        private void Received(FLDLogRecord record)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_hub == null) return;
                _items.Add(record);
                while (_items.Count > VisibleLimit) _items.RemoveAt(0);
                if (Paused.IsChecked != true && _view != null && Accept(record))
                    LogTable.ScrollIntoView(record);
            }));
        }

        private bool Accept(object item)
        {
            FLDLogRecord log = item as FLDLogRecord;
            if (log == null) return false;
            int level = LevelFilter == null ? 0 : LevelFilter.SelectedIndex;
            if (level > 0 && (int)log.Level != level - 1) return false;
            string term = SearchText == null ? "" : SearchText.Text.Trim();
            if (term.Length == 0) return true;
            return Contains(log.Message, term) || Contains(log.ModuleId, term)
                || Contains(log.DedicatedName, term) || Contains(log.CorrelationId, term);
        }

        private static bool Contains(string value, string term)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_view != null) _view.Refresh();
        }

        private void FilterTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_view != null) _view.Refresh();
        }

        private void ClearClicked(object sender, RoutedEventArgs e) { _items.Clear(); }
    }
}
