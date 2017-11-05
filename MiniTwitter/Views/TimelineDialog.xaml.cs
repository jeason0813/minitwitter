﻿using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using MiniTwitter.Models.Net.Twitter;
using MiniTwitter.Properties;
using MiniTwitter.Extensions;

namespace MiniTwitter.Views
{
    /// <summary>
    /// TimelineDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class TimelineDialog : Window
    {
        public TimelineDialog()
        {
            InitializeComponent();

            if (Settings.Default.IsClearTypeEnabled)
            {
                TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            }
        }

        private string _title;
        private ObservableCollection<Filter> filters;

        public Timeline Timeline { get; set; }

        public List[] Lists { get; set; }

        private void TimelineDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (Timeline == null)
            {
                Timeline = new Timeline { Type = TimelineType.User };
            }
            filters = new ObservableCollection<Filter>(Timeline.Filters);
            FilterListView.ItemsSource = filters;
            filters.BeginEdit();
            lists.ItemsSource = Lists;
            DataContext = Timeline;
            _title = Timeline.Name;
            BindingGroup.BeginEdit();

            switch (Timeline.Type)
            {
                case TimelineType.User:
                    filterRadio.IsChecked = true;
                    break;
                case TimelineType.Search:
                    searchRadio.IsChecked = true;
                    search.Text = Timeline.Tag;
                    break;
                case TimelineType.List:
                    listRadio.IsChecked = true;
                    lists.SelectedValue = Timeline.Tag;
                    break;
                default:
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BindingGroup.CommitEdit())
            {
                return;
            }

            Timeline.Filters.Clear();
            Timeline.Tag = "";

            if (listRadio.IsChecked ?? false)
            {
                Timeline.Type = TimelineType.List;
                Timeline.Tag = (string)lists.SelectedValue;
            }
            if (searchRadio.IsChecked ?? false)
            {
                Timeline.Type = TimelineType.Search;
                Timeline.Tag = search.Text;
            }
            if (filterRadio.IsChecked ?? false)
            {
                Timeline.Type = TimelineType.User;
                Timeline.Filters.AddRange(filters);
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            filters.CancelEdit();
            BindingGroup.CancelEdit();
            Timeline.Name = _title;
            DialogResult = false;
        }

        private void AddFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var filter = new Filter { Pattern = FilterTextBox.Text, Type = FilterType.None };
            filters.Add(filter);
            filter.BeginEdit();
            FilterListView.ScrollIntoView(filter);
            FilterTextBox.Clear();
        }

        private void DeleteFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (Filter)FilterListView.SelectedItem;

            if (item == null)
            {
                return;
            }

            filters.Remove(item);
        }
    }
}
