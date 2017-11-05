﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using MiniTwitter.Extensions;
using MiniTwitter.Models.Input;
using MiniTwitter.Models.Net;
using MiniTwitter.Models.Net.Twitter;
using MiniTwitter.Properties;
using MiniTwitter.Resources;
using MiniTwitter.Resources.languages.jp;
using MiniTwitter.Themes;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MiniTwitter.Views
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (Settings.Default.IsClearTypeEnabled)
            {
                TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            }
        }

        public string TargetValue { get; set; }

        private TwitterClient client = new TwitterClient("CONSUMER KEY", "CONSUMER SECRET");

        private volatile bool _isClosing = false;

        private ulong? _inReplyToStatusId = null;

        private readonly PopupWindow _popupWindow = new PopupWindow();
        private readonly NotifyIcon _notifyIcon = new NotifyIcon();

        private readonly GeoCoordinateWatcher _geoWatcher = new GeoCoordinateWatcher();

        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer _refreshReplyTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer _refreshMessageTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer _refreshListTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer _refreshSearchTimer = new DispatcherTimer(DispatcherPriority.Background);

        private readonly DispatcherTimer _quickSearchTimer = new DispatcherTimer(DispatcherPriority.Background);

        private readonly ObservableCollection<Timeline> _timelines = new ObservableCollection<Timeline>();

        public ObservableCollection<Timeline> Timelines
        {
            get { return _timelines; }
        }

        private readonly List<User> users = new List<User>();
        private readonly List<string> hashtags = new List<string>();

        private List[] _lists;

        public string StatusText
        {
            get { return (string)GetValue(StatusTextProperty); }
            set { SetValue(StatusTextProperty, value); }
        }

        public static readonly DependencyProperty StatusTextProperty =
                DependencyProperty.Register("StatusText", typeof(string), typeof(MainWindow), new PropertyMetadata(App.NAME + " " + App.VERSION));

        public enum RefreshTarget
        {
            All,
            Recent,
            Replies,
            Archive,
            Message,
            List,
            Search,
        }

        private void UpdateUsersList(IEnumerable<User> updateUsers)
        {
            var newUsers = updateUsers.Where(p => p != null && !users.Contains(p) && !string.IsNullOrEmpty(p.ScreenName)).ToList();
            foreach (var user in newUsers)
            {
                users.Add(user);
            }
            if (newUsers.Count != 0)
            {
                try
                {
                    users.Sort((x, y) => x.ScreenName.CompareTo(y.ScreenName));
                }
                catch
                {
                }
            }
        }

        private Regex _hashtagRegex = new Regex(@"(^|\s+)#(?<hash>[-_a-zA-Z0-9]{2,20})", RegexOptions.Compiled);

        private void UpdateHashtagList(IEnumerable<Status> updateStatuses)
        {
            int count = 0;

            foreach (var status in updateStatuses)
            {
                if (string.IsNullOrEmpty(status.Text))
                {
                    continue;
                }

                var matches = _hashtagRegex.Matches(status.Text);

                foreach (Match match in matches)
                {
                    var hashtag = match.Groups["hash"].Value;

                    if (!hashtags.Contains(hashtag))
                    {
                        count++;
                        hashtags.Add(hashtag);
                    }
                }
            }

            if (count > 0)
            {
                try
                {
                    hashtags.Sort();
                }
                catch
                {
                }
            }
        }

        private void InitializeTimeline()
        {
            // 初期タイムラインを作成
            Timelines.Add(new Timeline { Type = TimelineType.Recent, Name = "Recent" });
            Timelines.Add(new Timeline { Type = TimelineType.Replies, Name = "Replies" });
            Timelines.Add(new Timeline { Type = TimelineType.Archive, Name = "Archive" });
            Timelines.Add(new Timeline { Type = TimelineType.Message, Name = "Message" });
            // ユーザータイムラインを作成
            foreach (var item in Settings.Default.Timelines)
            {
                Timelines.Add(item);
            }
            // タイムラインをソート
            Timelines.Sort(Settings.Default.SortCategory, Settings.Default.SortDirection);
            _popupWindow.Timeline.Sort(Settings.Default.SortCategory, Settings.Default.SortDirection);
        }

        private void InitializeFilter()
        {
            var replies = Timelines.TypeAt(TimelineType.Replies);
            replies.Filters.Clear();
            replies.Filters.Add(new Filter { Type = FilterType.RegexText, Pattern = string.Format(@"@{0}[^a-zA-Z_0-9]", client.LoginedUser.ScreenName) });
            var archive = Timelines.TypeAt(TimelineType.Archive);
            archive.Filters.Clear();
            archive.Filters.Add(new Filter { Type = FilterType.Name, Pattern = client.LoginedUser.ScreenName });
        }

        private void InitializeAutoRefresh()
        {
            if (!Settings.Default.UseUserStream)
            {
                _refreshTimer.IsEnabled = Settings.Default.EnableAutoRefresh;
                _refreshTimer.Interval = TimeSpan.FromSeconds(Settings.Default.RefreshTick);
                _refreshReplyTimer.IsEnabled = Settings.Default.EnableAutoRefresh;
                _refreshReplyTimer.Interval = TimeSpan.FromMinutes(Settings.Default.RefreshReplyTick);
            }
            _refreshMessageTimer.IsEnabled = Settings.Default.EnableAutoRefresh;
            _refreshMessageTimer.Interval = TimeSpan.FromMinutes(Settings.Default.RefreshMessageTick);
            _refreshListTimer.IsEnabled = Settings.Default.EnableAutoRefresh;
            _refreshListTimer.Interval = TimeSpan.FromMinutes(Settings.Default.RefreshListTick);
            _refreshSearchTimer.IsEnabled = Settings.Default.EnableAutoRefresh;
            _refreshSearchTimer.Interval = TimeSpan.FromMinutes(Settings.Default.RefreshSearchTick);
        }

        private void InitializeTwitter()
        {
            client.ConvertShortUrl = true;
            if (Settings.Default.UseProxy)
            {
                if (Settings.Default.UseIEProxy)
                {
                    client.Proxy = WebRequest.GetSystemWebProxy();
                }
                else
                {
                    IWebProxy proxy;
                    if (Settings.Default.ProxyPortNumber.IsNullOrEmpty())
                    {
                        proxy = new WebProxy(Settings.Default.ProxyAddress);
                    }
                    else
                    {
                        proxy = new WebProxy(Settings.Default.ProxyAddress + ":" + Settings.Default.ProxyPortNumber);
                    }
                    if (!Settings.Default.ProxyUsername.IsNullOrEmpty() && !Settings.Default.ProxyPassword.IsNullOrEmpty())
                    {
                        proxy.Credentials = new NetworkCredential(Settings.Default.ProxyUsername, Settings.Default.ProxyPassword);
                    }
                    client.Proxy = proxy;
                }
            }
            client.Footer = Settings.Default.EnableTweetFooter ? Settings.Default.TweetFooter : string.Empty;
        }

        private void InitializePopupWindow()
        {
            _popupWindow.Location = Settings.Default.PopupLocation;
            _popupWindow.CloseTick = Settings.Default.PopupCloseTick;
        }

        /// <summary>
        /// キーボードショートカットを初期化、登録する
        /// </summary>
        private void InitializeKeyboardShortcut()
        {
            if (Settings.Default.KeyMapping == null)
            {
                if (KeyMapping.KeyMappings.Count == 0)
                {
                    return;
                }
                var keyMapping = KeyMapping.GetKeyMapping(0);
                Settings.Default.KeyMapping = keyMapping.Name;
                Settings.Default.KeyBindings.Clear();
                foreach (var item in keyMapping.KeyBindings)
                {
                    Settings.Default.KeyBindings.Add(item);
                }
            }
            InputBindings.Clear();
            TweetTextBox.InputBindings.Clear();
            TimelineTabControl.InputBindings.Clear();

            foreach (var keyBinding in Settings.Default.KeyBindings)
            {
                if (keyBinding.Key == Key.None)
                {
                    continue;
                }
                try
                {
                    var inputBinding = new InputBinding(keyBinding.Action.ToCommand(), new KeyGesture(keyBinding.Key, keyBinding.ModifierKeys));
                    switch (keyBinding.ActionSpot)
                    {
                        case KeyActionSpot.All:
                            InputBindings.Add(inputBinding);
                            break;
                        case KeyActionSpot.TweetTextBox:
                            TweetTextBox.InputBindings.Add(inputBinding);
                            break;
                        case KeyActionSpot.Timeline:
                            TimelineTabControl.InputBindings.Add(inputBinding);
                            break;
                        case KeyActionSpot.Global:
                            break;
                    }
                }
                catch
                {
                    keyBinding.Key = Key.None;
                    keyBinding.ModifierKeys = ModifierKeys.None;
                }
            }
        }

        private void Login()
        {
            // ログインを開始
            this.AsyncInvoke(() => StatusText = Resource_jp.logged_in);
            ThreadPool.QueueUserWorkItem(LoginCallback);
        }

        private void LoginCallback(object state)
        {
            if (string.IsNullOrEmpty(Settings.Default.Token) || string.IsNullOrEmpty(Settings.Default.TokenSecret))
            {
                string token = null;
                string tokenSecret = null;

                if (string.IsNullOrEmpty(Settings.Default.Username) || string.IsNullOrEmpty(Settings.Default.Password))
                {
                    this.Invoke(() => StatusText = Resource_jp.invalid_credentials);
                    return;
                }
                else if (!client.GetAccessToken(Settings.Default.Username, Settings.Default.Password, ref token, ref tokenSecret))
                {
                    this.Invoke(() => StatusText = Resource_jp.OAuth_authentication_failed);
                    return;
                }

                Settings.Default.Token = token;
                Settings.Default.TokenSecret = tokenSecret;
            }

            // 設定に従いログイン開始
            var result = client.Login(Settings.Default.Token, Settings.Default.TokenSecret);
            if (result == null)
            {
                this.Invoke(() => StatusText = Resource_jp.OAuth_authentication_failed);

                Settings.Default.Token = null;
                Settings.Default.TokenSecret = null;
                return;
            }
            if (!result.Value || !client.IsLogined)
            {
                // ログインに失敗
                this.Invoke(() => StatusText = Resource_jp.login_failed);

                Settings.Default.Token = null;
                Settings.Default.TokenSecret = null;

                // 再ログイン用にタイマーを仕込む
                _refreshTimer.IsEnabled = true;
                _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            }
            else
            {
                // すべてのタイムラインの項目を削除する
                Timelines.ClearAll();
                // フィルタを初期化
                InitializeFilter();
                // すべてのタイムラインを取得する
                RefreshTimeline(RefreshTarget.All);
                // 自動更新タイマーを初期化
                InitializeAutoRefresh();
                if (Settings.Default.UseUserStream)
                {
                    client.ChirpUserStream();
                }
            }
        }

        private void SetStatusMessage(bool isSuccess)
        {
            this.AsyncInvoke(p => StatusText = p, isSuccess ? DateTime.Now.ToString("G") + Resource_jp.get_complete : Resource_jp.failed_to_get_timeline);
        }

        private void RefreshTimeline(RefreshTarget target)
        {
            // タイムラインを取得
            Status[] statuses;
            Status[] notAuthorStatuses;
            var isKeyword = false;
            switch (target)
            {
                case RefreshTarget.All:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_timeline_for_all);
                    // Recent を取得する
                    statuses = client.RecentTimeline;
                    // 取得できたか確認する
                    if (statuses == null)
                    {
                        SetStatusMessage(false);
                        return;
                    }
                    if (Settings.Default.IgnoreRegex != null)
                    {
                        statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
                    }
                    // ユーザーリストを更新
                    UpdateUsersList(statuses.Select(p => p.Sender).Distinct());
                    UpdateHashtagList(statuses);
                    // ステータスを反映させる
                    Timelines.Update(statuses);
                    // 返信タイムラインを反映させる
                    statuses = client.RepliesTimeline;
                    if (Settings.Default.IgnoreRegex != null)
                    {
                        statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
                    }
                    // ユーザーリストを更新
                    if (statuses != null)
                    {
                        UpdateUsersList(statuses.Select(p => p.Sender).Distinct());
                        UpdateHashtagList(statuses);
                    }
                    // ステータスを反映させる
                    Timelines.Update(TimelineType.Replies, statuses);
                    Timelines.Update(TimelineType.User, statuses);
                    // アーカイブを反映させる
                    statuses = client.ArchiveTimeline;
                    if (statuses != null)
                    {
                        if (Settings.Default.IgnoreRegex != null)
                        {
                            statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
                        }
                        UpdateHashtagList(statuses);
                    }
                    // ステータスを反映させる
                    Timelines.Update(TimelineType.Archive, statuses);
                    Timelines.Update(TimelineType.User, statuses);
                    // メッセージを受信
                    Timelines.Update(TimelineType.Message, client.ReceivedMessages);
                    Timelines.Update(TimelineType.Message, client.SentMessages);
                    // リストを取得
                    _lists = client.Lists;
                    foreach (var timeline in Timelines.Where(p => p.Type == TimelineType.List))
                    {
                        statuses = client.GetListStatuses(timeline.Tag, timeline.SinceId);
                        if (statuses == null)
                        {
                            continue;
                        }
                        this.Invoke(p => timeline.Update(p), statuses);
                    }
                    foreach (var timeline in Timelines.Where(p => p.Type == TimelineType.Search))
                    {
                        statuses = client.Search(timeline.Tag, timeline.SinceId);
                        if (statuses == null)
                        {
                            continue;
                        }
                        this.Invoke(p => timeline.Update(p), statuses);
                    }
                    // 取得完了
                    SetStatusMessage(true);
                    return;
                case RefreshTarget.Recent:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_furendotaimurain);
                    statuses = client.RecentTimeline;
                    if (statuses != null)
                    {
                        statuses = Timelines.Normalize(TimelineType.Recent, statuses);
                        // ユーザーリストを更新
                        UpdateUsersList(statuses.Select(p => p.Sender).Distinct());
                        UpdateHashtagList(statuses);
                    }
                    break;
                case RefreshTarget.Replies:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_response_timeline);
                    statuses = client.RepliesTimeline;
                    if (statuses != null)
                    {
                        statuses = Timelines.Normalize(TimelineType.Replies, statuses);
                        // ユーザーリストを更新
                        UpdateUsersList(statuses.Select(p => p.Sender).Distinct());
                        UpdateHashtagList(statuses);
                    }
                    break;
                case RefreshTarget.Archive:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_yuzataimurain);
                    statuses = client.ArchiveTimeline;
                    if (statuses == null)
                    {
                        SetStatusMessage(false);
                        return;
                    }
                    Timelines.Update(statuses);
                    SetStatusMessage(true);
                    return;
                case RefreshTarget.Message:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_the_direct);
                    var messages = client.ReceivedMessages;
                    if (messages == null)
                    {
                        SetStatusMessage(false);
                        return;
                    }
                    var normalized = Timelines.Normalize(messages);
                    if (normalized.Length > 0 && !_isSilentMode)
                    {
                        if (Settings.Default.EnablePopup)
                        {
                            _popupWindow.Show(normalized);
                        }
                        var sound = Settings.Default.SoundBindings.Where(p => p.IsEnabled && p.Action == SoundAction.Message).FirstOrDefault();
                        if (sound != null)
                        {
                            try
                            {
                                new SoundPlayer(sound.FileName).Play();
                            }
                            catch { }
                        }
                    }
                    Timelines.Update(TimelineType.Message, messages);
                    SetStatusMessage(true);
                    return;
                case RefreshTarget.List:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_list);
                    foreach (var timeline in Timelines.Where(p => p.Type == TimelineType.List))
                    {
                        statuses = client.GetListStatuses(timeline.Tag, timeline.SinceId);
                        if (statuses == null)
                        {
                            SetStatusMessage(false);
                            continue;
                        }
                        statuses = timeline.Normalize(statuses);
                        if (Settings.Default.EnableUnreadManager)
                        {
                            Array.ForEach(statuses, item => item.IsNewest = !item.IsAuthor);
                        }
                        if (Settings.Default.IgnoreRegex != null)
                        {
                            statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
                        }
                        notAuthorStatuses = Timelines.TypeAt(TimelineType.Recent).Normalize(statuses.Where(p => !p.IsAuthor));
                        if (notAuthorStatuses.Length > 0 && !_isSilentMode)
                        {
                            if (Settings.Default.EnablePopup)
                            {
                                if (!Settings.Default.PopupOnlyNotActive || this.Invoke<bool>(() => !IsActive))
                                {
                                    if (Settings.Default.PopupOnlyFavorite)
                                    {
                                        if (Settings.Default.FavoriteRegex != null)
                                        {
                                            _popupWindow.Show(notAuthorStatuses.Where(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)));
                                        }
                                    }
                                    else
                                    {
                                        _popupWindow.Show(notAuthorStatuses);
                                    }
                                }
                            }
                            var action = notAuthorStatuses.Any(p => Regex.IsMatch(p.Text, string.Format(@"{0}[^a-zA-Z_0-9]", client.LoginedUser.ScreenName))) ? SoundAction.Reply : SoundAction.Status;
                            var sound = Settings.Default.SoundBindings.Where(p => p.IsEnabled && p.Action == action).FirstOrDefault();
                            if (Settings.Default.FavoriteRegex != null && notAuthorStatuses.Any(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)))
                            {
                                isKeyword = true;
                            }
                        }
                        var recent = Timelines.TypeAt(TimelineType.Recent);
                        for (int i = 0; i < statuses.Length; ++i)
                        {
                            var item = statuses[i];

                            var status = (Status)recent.Items.FirstOrDefault(p => p.Id == item.Id);

                            if (status != null)
                            {
                                statuses[i] = status;
                            }
                        }
                        this.Invoke(p => timeline.Update(p), statuses);
                    }
                    if (isKeyword)
                    {
                        var keywordSound = Settings.Default.SoundBindings.FirstOrDefault(p => p.IsEnabled && p.Action == SoundAction.Keyword);

                        if (keywordSound != null)
                        {
                            try
                            {
                                new SoundPlayer(keywordSound.FileName).Play();
                            }
                            catch { }
                        }
                    }
                    SetStatusMessage(true);
                    return;
                case RefreshTarget.Search:
                    this.AsyncInvoke(() => StatusText = Resource_jp.get_results);
                    foreach (var timeline in Timelines.Where(p => p.Type == TimelineType.Search))
                    {
                        statuses = client.Search(timeline.Tag, timeline.SinceId);
                        if (statuses == null)
                        {
                            SetStatusMessage(false);
                            continue;
                        }
                        statuses = timeline.Normalize(statuses);
                        if (Settings.Default.EnableUnreadManager)
                        {
                            Array.ForEach(statuses, item => item.IsNewest = !item.IsAuthor);
                        }
                        if (Settings.Default.IgnoreRegex != null)
                        {
                            statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
                        }
                        notAuthorStatuses = Timelines.TypeAt(TimelineType.Recent).Normalize(statuses.Where(p => !p.IsAuthor));
                        if (notAuthorStatuses.Length > 0 && !_isSilentMode)
                        {
                            if (Settings.Default.EnablePopup)
                            {
                                if (!Settings.Default.PopupOnlyNotActive || this.Invoke<bool>(() => !IsActive))
                                {
                                    if (Settings.Default.PopupOnlyFavorite)
                                    {
                                        if (Settings.Default.FavoriteRegex != null)
                                        {
                                            _popupWindow.Show(notAuthorStatuses.Where(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)));
                                        }
                                    }
                                    else
                                    {
                                        _popupWindow.Show(notAuthorStatuses);
                                    }
                                }
                            }
                            var action = notAuthorStatuses.Any(p => Regex.IsMatch(p.Text, string.Format(@"{0}[^a-zA-Z_0-9]", client.LoginedUser.ScreenName))) ? SoundAction.Reply : SoundAction.Status;
                            var sound = Settings.Default.SoundBindings.Where(p => p.IsEnabled && p.Action == action).FirstOrDefault();
                            if (Settings.Default.FavoriteRegex != null && notAuthorStatuses.Any(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)))
                            {
                                isKeyword = true;
                            }
                        }
                        var recent = Timelines.TypeAt(TimelineType.Recent);
                        for (int i = 0; i < statuses.Length; ++i)
                        {
                            var item = statuses[i];

                            var status = (Status)recent.Items.FirstOrDefault(p => p.Id == item.Id);

                            if (status != null)
                            {
                                statuses[i] = status;
                            }
                        }
                        this.Invoke(p => timeline.Update(p), statuses);
                    }
                    if (isKeyword)
                    {
                        var keywordSound = Settings.Default.SoundBindings.FirstOrDefault(p => p.IsEnabled && p.Action == SoundAction.Keyword);

                        if (keywordSound != null)
                        {
                            try
                            {
                                new SoundPlayer(keywordSound.FileName).Play();
                            }
                            catch { }
                        }
                    }
                    SetStatusMessage(true);
                    return;
                default:
                    return;
            }
            // 取得できたか確認する
            if (statuses == null)
            {
                SetStatusMessage(false);
                return;
            }
            if (Settings.Default.EnableUnreadManager)
            {
                Array.ForEach(statuses, item => item.IsNewest = !item.IsAuthor);
            }
            if (Settings.Default.IgnoreRegex != null)
            {
                statuses = statuses.Where(p => !Settings.Default.IgnoreRegex.IsMatch(p.Text)).ToArray();
            }
            notAuthorStatuses = statuses.Where(p => !p.IsAuthor).ToArray();
            if (notAuthorStatuses.Length > 0 && !_isSilentMode)
            {
                if (Settings.Default.EnablePopup)
                {
                    if (!Settings.Default.PopupOnlyNotActive || this.Invoke<bool>(() => !IsActive))
                    {
                        if (Settings.Default.PopupOnlyFavorite)
                        {
                            if (Settings.Default.FavoriteRegex != null)
                            {
                                _popupWindow.Show(notAuthorStatuses.Where(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)));
                            }
                        }
                        else
                        {
                            _popupWindow.Show(notAuthorStatuses);
                        }
                    }
                }
                var action = notAuthorStatuses.Any(p => Regex.IsMatch(p.Text, string.Format(@"{0}[^a-zA-Z_0-9]", client.LoginedUser.ScreenName))) ? SoundAction.Reply : SoundAction.Status;
                var sound = Settings.Default.SoundBindings.Where(p => p.IsEnabled && p.Action == action).FirstOrDefault();
                if (Settings.Default.FavoriteRegex != null && notAuthorStatuses.Any(p => Settings.Default.FavoriteRegex.IsMatch(p.Text)))
                {
                    var keywordSound = Settings.Default.SoundBindings.FirstOrDefault(p => p.IsEnabled && p.Action == SoundAction.Keyword);

                    if (keywordSound != null)
                    {
                        sound = keywordSound;
                    }
                }
                if (sound != null)
                {
                    try
                    {
                        new SoundPlayer(sound.FileName).Play();
                    }
                    catch { }
                }
            }
            this.Invoke(() =>
            {
                if (TimelineTabControl.SelectedItem != null)
                {
                    ((Timeline)TimelineTabControl.SelectedItem).VerticalOffset = _mainViewer.VerticalOffset;
                }
            });
            Timelines.Update(statuses);
            this.AsyncInvoke(() =>
            {
                if (TimelineTabControl.SelectedItem != null)
                {
                    _mainViewer.ScrollToVerticalOffset(((Timeline)TimelineTabControl.SelectedItem).VerticalOffset);
                }
            });
            // 更新完了
            SetStatusMessage(true);
        }

        private void RefreshTimelineAsync(RefreshTarget target)
        {
            if (!client.IsLogined)
            {
                // ログインしていないので再ログイン
                Login();
                return;
            }
            ThreadPool.QueueUserWorkItem(state => RefreshTimeline((RefreshTarget)state), target);
        }

        private void ForceActivate(bool isTextFocus = true)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (!IsVisible)
            {
                Show();
            }

            Activate();

            if (isTextFocus)
            {
                TweetTextBox.Focus();
            }
        }

        private void MainWindowInitialized(object sender, EventArgs e)
        {
            if (Settings.Default == null)
            {
                return;
            }
            ServicePointManager.DefaultConnectionLimit = 50;
            // ウィンドウの位置と状態を復元
            if (Settings.Default.Location.X != 0 || Settings.Default.Location.Y != 0)
            {
                Left = Settings.Default.Location.X;
                Top = Settings.Default.Location.Y;
            }
            if (Settings.Default.Size.Width != 0 || Settings.Default.Size.Height != 0)
            {
                Width = Settings.Default.Size.Width;
                Height = Settings.Default.Size.Height;
            }
            WindowState = Settings.Default.WindowState;
            // Twitter クライアントのイベントを登録
            client.Updated += new EventHandler<UpdateEventArgs>(TwitterClientUpdated);
            client.UpdateFailure += new EventHandler(TwitterClientUpdateFailure);

            client.UserStreamUpdated += (_, __) =>
            {
                this.Invoke(() =>
                {
                    var item = __.Status;

                    if (item == null)
                    {
                        return;
                    }

                    UpdateHashtagList(new[] { item });
                    UpdateUsersList(new[] { item.Sender });

                    if (__.Action == StatusAction.Deleted)
                    {
                        var status = (Status)Timelines.TypeAt(TimelineType.Recent).Items.FirstOrDefault(p => p.Id == item.Id);

                        if (status != null)
                        {
                            Timelines.Remove(status);
                        }
                        return;
                    }
                    if (__.Action == StatusAction.Favorited)
                    {
                        var status = (Status)Timelines.TypeAt(TimelineType.Recent).Items.FirstOrDefault(p => p.Id == item.Id);

                        if (status != null)
                        {
                            if (status.Sender.Id == item.Sender.Id)
                            {
                                status.Favorited = true;
                            }
                        }
                        return;
                    }
                    else if (__.Action == StatusAction.Unfavorited)
                    {
                        var status = (Status)Timelines.TypeAt(TimelineType.Recent).Items.FirstOrDefault(p => p.Id == item.Id);

                        if (status != null)
                        {
                            if (status.Sender.Id == item.Sender.Id)
                            {
                                status.Favorited = false;
                            }
                        }
                        return;
                    }

                    if (Settings.Default.EnableUnreadManager)
                    {
                        item.IsNewest = !item.IsAuthor;
                    }

                    if (Settings.Default.IgnoreRegex != null)
                    {
                        if (Settings.Default.IgnoreRegex.IsMatch(item.Text))
                        {
                            return;
                        }
                    }

                    if (!item.IsAuthor && !_isSilentMode)
                    {
                        if (Settings.Default.EnablePopup)
                        {
                            if (!Settings.Default.PopupOnlyNotActive || this.Invoke<bool>(() => !IsActive))
                            {
                                if (Settings.Default.PopupOnlyFavorite)
                                {
                                    if (Settings.Default.FavoriteRegex != null)
                                    {
                                        if (Settings.Default.FavoriteRegex.IsMatch(item.Text))
                                        {
                                            _popupWindow.Show(new[] { item });
                                        }
                                    }
                                }
                                else
                                {
                                    _popupWindow.Show(new[] { item });
                                }
                            }
                        }

                        var action = Regex.IsMatch(item.Text, string.Format(@"{0}[^a-zA-Z_0-9]", client.LoginedUser.ScreenName)) ? SoundAction.Reply : SoundAction.Status;

                        var sound = Settings.Default.SoundBindings.Where(p => p.IsEnabled && p.Action == action).FirstOrDefault();

                        if (Settings.Default.FavoriteRegex != null && Settings.Default.FavoriteRegex.IsMatch(item.Text))
                        {
                            var keywordSound = Settings.Default.SoundBindings.FirstOrDefault(p => p.IsEnabled && p.Action == SoundAction.Keyword);

                            if (keywordSound != null)
                            {
                                sound = keywordSound;
                            }
                        }
                        if (sound != null)
                        {
                            try
                            {
                                new SoundPlayer(sound.FileName).Play();
                            }
                            catch { }
                        }
                    }

                    ((Timeline)TimelineTabControl.SelectedItem).VerticalOffset = _mainViewer.VerticalOffset;

                    if (!item.IsMessage)
                    {
                        Timelines.Update(new[] { item });
                        UpdateHashtagList(new[] { (Status)item });
                    }
                    else
                    {
                        Timelines.Update(TimelineType.Message, new[] { item });
                    }

                    _mainViewer.ScrollToVerticalOffset(((Timeline)TimelineTabControl.SelectedItem).VerticalOffset);
                });
            };
            // タイマーを初期化
            _refreshTimer.Tick += (_, __) => RefreshTimelineAsync(RefreshTarget.Recent);
            _refreshReplyTimer.Tick += (_, __) => RefreshTimelineAsync(RefreshTarget.Replies);
            _refreshMessageTimer.Tick += (_, __) => RefreshTimelineAsync(RefreshTarget.Message);
            _refreshListTimer.Tick += (_, __) => RefreshTimelineAsync(RefreshTarget.List);
            _refreshSearchTimer.Tick += (_, __) => RefreshTimelineAsync(RefreshTarget.Search);

            _quickSearchTimer.Interval = TimeSpan.FromMilliseconds(250);
            _quickSearchTimer.Tick += new EventHandler(QuickSearchTimer_Tick);
            // 通知領域アイコンを初期化
            _notifyIcon.Text = App.NAME;
            _notifyIcon.Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri(@".\Resources\MiniTwitter_small.ico", UriKind.Relative)).Stream);
            _notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(NotifyIconMouseClick);
            _notifyIcon.Visible = Settings.Default.EnableNotifyIcon;
            // ポップアップウィンドウのイベントを登録
            _popupWindow.CommandBindings.AddRange(new[]
                {
                    new CommandBinding(Commands.Reply, ReplyCommandExecuted),
                    new CommandBinding(Commands.ReTweet, ReTweetCommandExecuted),
                    new CommandBinding(Commands.ReTweetApi, ReTweetApiCommand_Executed),
                    new CommandBinding(Commands.ReplyMessage, ReplyMessageCommand_Executed),
                    new CommandBinding(Commands.Delete, DeleteCommand_Executed),
                    new CommandBinding(Commands.Favorite, FavoriteCommand_Executed),
                    new CommandBinding(Commands.MoveToStatusPage, MoveToStatusPageCommand_Executed),
                    new CommandBinding(Commands.MoveToUserPage, MoveToUserPageCommand_Executed),
                    new CommandBinding(Commands.InReplyTo, InReplyToCommand_Executed),
                });
            _popupWindow.MouseLeftButtonDown += new MouseButtonEventHandler(PopupWindowMouseLeftButtonDown);

            _uploader.UploadCompleted += new EventHandler<TwitpicUploadCompletedEventArgs>(Uploader_UploadCompleted);
            // タイムラインタブを作成
            InitializeTimeline();
            // プロキシサーバの設定を反映
            InitializeTwitter();
            // ポップアップウィンドウを初期化
            InitializePopupWindow();
            // キーボードショートカットを初期化
            InitializeKeyboardShortcut();
        }

        private void PopupWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ForceActivate();
        }

        private void NotifyIconMouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ForceActivate();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Activate();
                var contextMenu = (ContextMenu)FindResource("notifyMenu");
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
                contextMenu.IsOpen = true;
            }
        }

        private ScrollViewer _mainViewer;

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // ScrollViewer を取得する
            Func<DependencyObject, ScrollViewer> getChildVisual = null;
            getChildVisual = dobj =>
            {
                if (dobj is ScrollViewer) return dobj as ScrollViewer;
                int count = VisualTreeHelper.GetChildrenCount(dobj);
                for (int i = 0; i < count; i++)
                {
                    var ret = getChildVisual(VisualTreeHelper.GetChild(dobj, i));
                    if (ret != null) return ret;
                }
                return null;
            };
            var sv = getChildVisual(TimelineTabControl);
            if (sv == null) return;

            _mainViewer = sv;
            _mainViewer.PreviewMouseWheel += (_, __) =>
                {
                    __.Handled = true;

                    if (__.Delta < 0)
                    {
                        _mainViewer.LineDown();
                    }
                    else
                    {
                        _mainViewer.LineUp();
                    }
                };

            if ((string.IsNullOrEmpty(Settings.Default.Username) || string.IsNullOrEmpty(Settings.Default.Password)) && (string.IsNullOrEmpty(Settings.Default.Token) || string.IsNullOrEmpty(Settings.Default.TokenSecret)))
            {
                SettingButtonClick(null, null);
            }
            else
            {
                // Twitter へログイン
                Login();
            }
        }

        private void MainWindowActivated(object sender, EventArgs e)
        {
            TweetTextBox.Focus();
        }

        private void TweetTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }
            if (e.Key == Key.Enter)
            {
                var index = TweetTextBox.CaretIndex;
                TweetTextBox.Text = TweetTextBox.Text.Insert(index, Environment.NewLine);
                TweetTextBox.CaretIndex = index + Environment.NewLine.Length;
                e.Handled = true;
            }
        }

        private void TweetTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (popup.IsOpen)
            {
                if (e.Key == Key.Up)
                {
                    if (usersListBox.SelectedIndex > 0)
                    {
                        usersListBox.SelectedIndex--;
                        usersListBox.ScrollIntoView(usersListBox.SelectedItem);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    int count = usersListBox.ItemsSource is List<User> ? ((List<User>)usersListBox.ItemsSource).Count : ((List<string>)usersListBox.ItemsSource).Count;
                    if (usersListBox.SelectedIndex < count)
                    {
                        usersListBox.SelectedIndex++;
                    }
                    usersListBox.ScrollIntoView(usersListBox.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    string text = usersListBox.SelectedItem is User ? ((User)usersListBox.SelectedItem).ScreenName + " " : (string)usersListBox.SelectedItem;

                    if (usersListBox.SelectedItem is string && _startIndex == 1)
                    {
                        text += " ";
                    }

                    TweetTextBox.Text = TweetTextBox.Text.Remove(_startIndex, TweetTextBox.CaretIndex - _startIndex).Insert(_startIndex, text);
                    TweetTextBox.CaretIndex = _startIndex + text.Length;

                    popup.IsOpen = false;

                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    popup.IsOpen = false;

                    e.Handled = true;
                }
            }
        }

        private void TweetTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (popup.IsOpen)
            {
                if (_startIndex > TweetTextBox.CaretIndex || (_startIndex + _addLength) < TweetTextBox.CaretIndex)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private int _startIndex = 0;
        private int _addLength = 0;

        private bool _isUser = false;

        private void TweetTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            var change = e.Changes.FirstOrDefault();

            if (change == null)
            {
                return;
            }

            if (popup.IsOpen)
            {
                _addLength += change.AddedLength;
                _addLength -= change.RemovedLength;

                if (_addLength < 0)
                {
                    return;
                }

                var text = TweetTextBox.Text.Substring(_startIndex, _addLength);

                if (_isUser)
                {
                    var filteredUsers = users.Where(p => p.ScreenName.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1).ToList();

                    if (filteredUsers.Count != 0)
                    {
                        usersListBox.ItemsSource = filteredUsers;

                        usersListBox.SelectedIndex = 0;
                        usersListBox.ScrollIntoView(usersListBox.SelectedItem);
                    }
                    else
                    {
                        popup.IsOpen = false;
                    }
                }
                else
                {
                    var filteredTags = hashtags.Where(p => p.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1).ToList();

                    if (filteredTags.Count != 0)
                    {
                        usersListBox.ItemsSource = filteredTags;

                        usersListBox.SelectedIndex = 0;
                        usersListBox.ScrollIntoView(usersListBox.SelectedItem);
                    }
                    else
                    {
                        popup.IsOpen = false;
                    }
                }
            }
            else
            {
                if (change.AddedLength == 0)
                {
                    return;
                }

                var text = TweetTextBox.Text.Substring(change.Offset, change.AddedLength);

                if (text == "@")
                {
                    _addLength = 0;
                    _startIndex = TweetTextBox.CaretIndex;

                    if (users.Count != 0)
                    {
                        usersListBox.ItemsSource = users;

                        usersListBox.SelectedIndex = 0;
                        usersListBox.ScrollIntoView(usersListBox.SelectedItem);

                        popup.PlacementTarget = TweetTextBox;
                        var rect = TweetTextBox.GetRectFromCharacterIndex(TweetTextBox.CaretIndex - 1);
                        rect.Offset(-24, 0);
                        popup.PlacementRectangle = rect;
                        popup.IsOpen = true;

                        _isUser = true;
                    }
                }
                else if (text == "#")
                {
                    _addLength = 0;
                    _startIndex = TweetTextBox.CaretIndex;

                    if (hashtags.Count != 0)
                    {
                        usersListBox.ItemsSource = hashtags;

                        usersListBox.SelectedIndex = 0;
                        usersListBox.ScrollIntoView(usersListBox.SelectedItem);

                        popup.PlacementTarget = TweetTextBox;
                        var rect = TweetTextBox.GetRectFromCharacterIndex(TweetTextBox.CaretIndex - 1);
                        rect.Offset(-4, 0);
                        popup.PlacementRectangle = rect;
                        popup.IsOpen = true;

                        _isUser = false;
                    }
                }
            }
        }

        private void UsersListBoxMouseUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;

            var item = listBox.SelectedItem;
            if (item == null)
            {
                return;
            }

            var element = (UIElement)listBox.ItemContainerGenerator.ContainerFromItem(item);
            if (element == null || !element.IsMouseOver)
            {
                return;
            }

            TweetTextBox.Focus();

            e.Handled = true;
        }

        private void UsersListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;

            var obj = listBox.SelectedItem;
            if (obj == null)
            {
                return;
            }

            var element = (UIElement)listBox.ItemContainerGenerator.ContainerFromItem(obj);
            if (element == null || !element.IsMouseOver)
            {
                return;
            }

            string text;

            if (_isUser)
            {
                text = ((User)obj).ScreenName + " ";
            }
            else
            {
                text = (string)obj;

                if (_startIndex == 1)
                {
                    text += " ";
                }
            }

            TweetTextBox.Text = TweetTextBox.Text.Remove(_startIndex, TweetTextBox.CaretIndex - _startIndex).Insert(_startIndex, text);

            popup.IsOpen = false;

            TweetTextBox.Focus();
            TweetTextBox.CaretIndex = _startIndex + text.Length;

            e.Handled = true;
        }

        private void MainWindowClosing(object sender, CancelEventArgs e)
        {
            if (Settings.Default == null)
            {
                return;
            }
            if (!_isClosing && Settings.Default.EnableNotifyIcon)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            _refreshTimer.Stop();
            _refreshReplyTimer.Stop();
            _refreshMessageTimer.Stop();
            _quickSearchTimer.Stop();
            // ウィンドウの設定を保存する
            Settings.Default.Location = RestoreBounds.Location;
            Settings.Default.Size = RestoreBounds.Size;
            Settings.Default.WindowState = WindowState;
            // ユーザータイムラインを保存する
            Settings.Default.Timelines.Clear();
            foreach (var item in Timelines.Where(p => p.Type == TimelineType.User || p.Type == TimelineType.List || p.Type == TimelineType.Search))
            {
                Settings.Default.Timelines.Add(item);
            }
            // その他のウィンドウを破棄
            _popupWindow.Close();
            _notifyIcon.Dispose();
        }

        private void TimelineListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (ITwitterItem item in e.AddedItems)
            {
                if (item.IsNewest)
                {
                    item.IsNewest = false;

                    foreach (var timeline in Timelines)
                    {
                        if (timeline.Items.Contains(item))
                        {
                            timeline.UnreadCount--;
                        }
                    }
                }
            }
        }

        private void TimelineListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;
            var item = (ITwitterItem)listBox.SelectedItem;
            if (item == null)
            {
                return;
            }
            var element = (UIElement)listBox.ItemContainerGenerator.ContainerFromItem(item);
            if (element == null || !element.IsMouseOver)
            {
                return;
            }
            if (item.IsMessage)
            {
                TweetTextBox.AppendText("D " + item.Sender.ScreenName + " ");
            }
            else
            {
                TweetTextBox.AppendText("@" + item.Sender.ScreenName + " ");
                _inReplyToStatusId = item.Id;
            }
            TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            TweetTextBox.Focus();
        }

        private void HomeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("http://twitter.com/home");
            }
            catch
            {
                MessageBox.Show(Resource_jp.failed_to_move, App.NAME);
            }
        }

        private void SettingButtonClick(object sender, RoutedEventArgs e)
        {
            var username = Settings.Default.Username;
            var password = Settings.Default.Password;
            var dialog = new SettingDialog { Owner = this };
            if (!(dialog.ShowDialog() ?? false))
            {
                return;
            }
            // 正規表現を組みなおす
            Settings.Default.InitializeKeywordRegex();
            // プロキシサーバの設定を反映
            InitializeTwitter();
            // ポップアップウィンドウを初期化
            InitializePopupWindow();
            // キーボードショートカットを初期化
            InitializeKeyboardShortcut();
            // 通知領域アイコン設定を変更
            _notifyIcon.Visible = Settings.Default.EnableNotifyIcon;
            // テーマが変更されているか確認
            Application.Current.ApplyTheme(Settings.Default.Theme);

            UpdateFooterMenu();

            if (Settings.Default.IsClearTypeEnabled)
            {
                TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            }
            else
            {
                TextOptions.SetTextRenderingMode(this, TextRenderingMode.Auto);
            }
            // ログインしているか判別
            if (!client.IsLogined)
            {
                // Twitter へログイン
                Login();
                return;
            }
            else
            {
                if (username != Settings.Default.Username || password != Settings.Default.Password)
                {
                    if (!string.IsNullOrEmpty(Settings.Default.Username) || !string.IsNullOrEmpty(Settings.Default.Password))
                    {
                        Settings.Default.Token = null;
                        Settings.Default.TokenSecret = null;

                        Login();
                    }
                }
                else
                {
                    InitializeAutoRefresh();
                }
            }
        }

        private ITwitterItem GetSelectedItem()
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            return timeline != null ? (ITwitterItem)timeline.View.CurrentItem : null;
        }

        private void UpdateCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var status = (string)e.Parameter ?? TweetTextBox.Text;
            if (string.IsNullOrEmpty(status) || !UpdateButton.IsEnabled)
            {
                return;
            }
            UpdateButton.IsEnabled = false;
            StatusText = Resource_jp.updating_status;
            //client.Update((string)e.Parameter ?? TweetTextBox.Text, in_reply_to_status_id);
            ThreadPool.QueueUserWorkItem(text => client.Update((string)text, _inReplyToStatusId, _latitude, _longitude), status);
        }

        private void TwitterClientUpdated(object sender, UpdateEventArgs e)
        {
            if (!Settings.Default.UseUserStream)
            {
                var item = e.Item;
                this.Invoke(() => ((Timeline)TimelineTabControl.SelectedItem).VerticalOffset = _mainViewer.VerticalOffset);
                if (!item.IsMessage)
                {
                    Timelines.Update(new[] { item });
                    UpdateHashtagList(new[] { (Status)item });
                }
                else
                {
                    Timelines.Update(TimelineType.Message, new[] { item });
                }
                this.Invoke(() => _mainViewer.ScrollToVerticalOffset(((Timeline)TimelineTabControl.SelectedItem).VerticalOffset));
            }
            else
            {
                var item = e.Item;
                this.Invoke(() => ((Timeline)TimelineTabControl.SelectedItem).VerticalOffset = _mainViewer.VerticalOffset);
                if (item.IsMessage)
                {
                    Timelines.Update(TimelineType.Message, new[] { item });
                }
                this.Invoke(() => _mainViewer.ScrollToVerticalOffset(((Timeline)TimelineTabControl.SelectedItem).VerticalOffset));
            }
            this.Invoke(() =>
            {
                UpdateButton.IsEnabled = true;
                _inReplyToStatusId = null;
                _latitude = null;
                _longitude = null;
                TweetTextBox.Clear();
                StatusText = Resource_jp.updated_status;
            });
        }

        private void TwitterClientUpdateFailure(object sender, EventArgs e)
        {
            this.Invoke(() =>
            {
                UpdateButton.IsEnabled = true;
                StatusText = Resource_jp.update_failed;
            });
        }

        private void RefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;

            if (timeline == null)
            {
                RefreshTimelineAsync(RefreshTarget.Recent);
            }
            else
            {
                switch (timeline.Type)
                {
                    case TimelineType.Recent:
                    case TimelineType.User:
                        RefreshTimelineAsync(RefreshTarget.Recent);
                        break;
                    case TimelineType.Replies:
                        RefreshTimelineAsync(RefreshTarget.Replies);
                        break;
                    case TimelineType.Archive:
                        RefreshTimelineAsync(RefreshTarget.Archive);
                        break;
                    case TimelineType.Message:
                        RefreshTimelineAsync(RefreshTarget.Message);
                        break;
                    case TimelineType.List:
                        RefreshTimelineAsync(RefreshTarget.List);
                        break;
                    case TimelineType.Search:
                        RefreshTimelineAsync(RefreshTarget.Search);
                        break;
                }
            }
        }

        private void ReplyCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            if (item is Status)
            {
                var screenName = item.Sender.ScreenName;

                if (((Status)item).ReTweetedStatus != null)
                {
                    screenName = ((Status)item).ReTweetedStatus.Sender.ScreenName;
                }

                TweetTextBox.Text = "@" + screenName + " " + TweetTextBox.Text;
                _inReplyToStatusId = item.Id;
            }
            else
            {
                TweetTextBox.Text = "D " + item.Sender.ScreenName + " " + TweetTextBox.Text;
            }
            TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            TweetTextBox.Focus();
            ForceActivate();
        }

        private void ReTweetCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (Status)e.Parameter ?? GetSelectedItem();
            if (Settings.Default.IsRetweetWithInReplyTo)
            {
                _inReplyToStatusId = item.Id;
            }
            else
            {
                _inReplyToStatusId = null;
            }
            TweetTextBox.Text = "RT @" + item.Sender.ScreenName + ": " + item.Text;
            TweetTextBox.CaretIndex = 0;
            TweetTextBox.Focus();
            ForceActivate();
        }

        private void ReTweetApiCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (Status)e.Parameter ?? GetSelectedItem();
            StatusText = Resource_jp.retweet_to;
            ThreadPool.QueueUserWorkItem(p =>
                {
                    var itm = (Status)p;
                    var status = client.ReTweet(itm.Id);
                    if (status != null)
                    {
                        this.Invoke(() => StatusText = Resource_jp.retweet_successful);

                        this.Invoke(() =>
                            {
                                itm.Id = status.Id;
                                itm.Text = status.Text;
                                itm.Source = status.Source;
                                itm.Sender = status.Sender;
                                itm.Favorited = status.Favorited;
                                itm.InReplyToStatusId = status.InReplyToStatusId;
                                itm.ReTweetedStatus = status.ReTweetedStatus;

                                Timelines.RefreshAll();
                            });
                    }
                    else
                    {
                        this.Invoke(() => StatusText = Resource_jp.retweet_failed);
                    }
                }, item);
        }

        private void ReplyMessageCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            TweetTextBox.Text = "D " + item.Sender.ScreenName + " " + TweetTextBox.Text;
            TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            TweetTextBox.Focus();
            ForceActivate();
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var twitterItem = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            if (twitterItem == null)
            {
                return;
            }
            if (MessageBox.Show(Resource_jp.delete_msg, App.NAME, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            ThreadPool.QueueUserWorkItem(state =>
                {
                    var item = (ITwitterItem)state;
                    if (!client.Delete(item))
                    {
                        return;
                    }
                    // タイムラインの項目も削除する
                    Timelines.Remove(item);
                }, twitterItem);
        }

        private void FavoriteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var twitterItem = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            if (twitterItem == null)
            {
                return;
            }
            ThreadPool.QueueUserWorkItem(state =>
                {
                    var item = (Status)state;
                    if (client.Favorite(item))
                    {
                        // お気に入りを切り替える
                        this.Invoke(() => item.Favorited = !item.Favorited);
                    }
                }, twitterItem);
        }

        private void TimelineStyleCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Settings.Default.TimelineStyle = (TimelineStyle)e.Parameter;
            Timelines.RefreshAll();
            _popupWindow.Timeline.View.Refresh();
        }

        private void MoveToUserPageCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            try
            {
                Process.Start("http://twitter.com/" + item.Sender.ScreenName);
            }
            catch
            {
                MessageBox.Show(Resource_jp.failed_to_move, App.NAME);
            }
        }

        private void MoveToStatusPageCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            try
            {
                Process.Start(string.Format("http://twitter.com/{0}/statuses/{1}", item.Sender.ScreenName, item.Id));
            }
            catch
            {
                MessageBox.Show(Resource_jp.failed_to_move, App.NAME);
            }
        }

        private void MoveToReplyPageCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (Status)(e.Parameter ?? GetSelectedItem());
            try
            {
                Process.Start(string.Format("http://twitter.com/{0}/statuses/{1}", item.InReplyToScreenName, item.InReplyToStatusId));
            }
            catch
            {
                MessageBox.Show(Resource_jp.failed_to_move, App.NAME);
            }
        }

        private void ReadAllCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Timelines.ReadAll();
        }

        private void ScrollUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            timeline.View.MoveCurrentToNext();
        }

        private void ScrollDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            timeline.View.MoveCurrentToPrevious();
        }

        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var text = (string)e.Parameter;
            if (text.IsNullOrEmpty())
            {
                var item = (ITwitterItem)GetSelectedItem();
                if (item == null)
                {
                    return;
                }
                text = item.Text;
            }
            Clipboard.SetText(text);
        }

        private void CopyUrlCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (ITwitterItem)e.Parameter ?? GetSelectedItem();
            Clipboard.SetText(string.Format("http://twitter.com/{0}/statuses/{1}", item.Sender.ScreenName, item.Id));
        }

        private void SortCategoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var category = (ListSortCategory)e.Parameter;
            if (Settings.Default.SortCategory == category)
            {
                return;
            }
            Settings.Default.SortCategory = category;
            Timelines.Sort(category, Settings.Default.SortDirection);
            _popupWindow.Timeline.Sort(category, Settings.Default.SortDirection);
        }

        private void SortDirectionCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var direction = (ListSortDirection)e.Parameter;
            if (Settings.Default.SortDirection == direction)
            {
                return;
            }
            Settings.Default.SortDirection = direction;
            Timelines.Sort(Settings.Default.SortCategory, direction);
            _popupWindow.Timeline.Sort(Settings.Default.SortCategory, direction);
        }

        private void AddTimelineCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new TimelineDialog { Owner = this, Lists = _lists };
            if (!(dialog.ShowDialog() ?? false))
            {
                return;
            }
            var timeline = dialog.Timeline;
            if (timeline.Type == TimelineType.User)
            {
                timeline.Update(Timelines.TypeAt(TimelineType.Recent).Items.Concat(Timelines.TypeAt(TimelineType.Replies).Items));
            }
            else if (timeline.Type == TimelineType.Search)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    this.Invoke(p => timeline.Update(p), client.Search(timeline.Tag, timeline.SinceId));
                });
            }
            else if (timeline.Type == TimelineType.List)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    this.Invoke(p => timeline.Update(p), client.GetListStatuses(timeline.Tag, timeline.SinceId));
                });
            }
            timeline.Sort(Settings.Default.SortCategory, Settings.Default.SortDirection);
            Timelines.Add(timeline);
            RefreshTimelineSettings();
        }

        private void EditTimelineCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            if (timeline.Type != TimelineType.User && timeline.Type != TimelineType.List && timeline.Type != TimelineType.Search)
            {
                MessageBox.Show(Resource_jp.can_not_edit_timeline, App.NAME);
                return;
            }
            var dialog = new TimelineDialog { Timeline = timeline, Owner = this, Lists = _lists };
            if (!(dialog.ShowDialog() ?? false))
            {
                return;
            }
            timeline.Clear();
            if (timeline.Type == TimelineType.User)
            {
                timeline.Update(Timelines.TypeAt(TimelineType.Recent).Items.Concat(Timelines.TypeAt(TimelineType.Replies).Items));
            }
            else if (timeline.Type == TimelineType.Search)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    this.Invoke(p => timeline.Update(p), client.Search(timeline.Tag, timeline.SinceId));
                });
            }
            else if (timeline.Type == TimelineType.List)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    this.Invoke(p => timeline.Update(p), client.GetListStatuses(timeline.Tag, timeline.SinceId));
                });
            }
            RefreshTimelineSettings();
        }

        private void DeleteTimelineCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            if (timeline.Type != TimelineType.User && timeline.Type != TimelineType.List && timeline.Type != TimelineType.Search)
            {
                return;
            }
            Timelines.Remove(timeline);
            RefreshTimelineSettings();
        }

        private void RefreshTimelineSettings()
        {
            // ユーザータイムラインを保存する
            Settings.Default.Timelines.Clear();
            foreach (var item in Timelines.Where(p => p.Type == TimelineType.User || p.Type == TimelineType.List || p.Type == TimelineType.Search))
            {
                Settings.Default.Timelines.Add(item);
            }
        }

        private void ClearTimelineCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)TimelineTabControl.SelectedItem;
            timeline.Clear();
        }


        private static readonly Regex schemaRegex = new Regex(@"^(https?:\/\/[-_.!~*'()a-zA-Z0-9;/?:@&=+$,%#]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (TweetTextBox.SelectionLength == 0)
                {
                    int index = TweetTextBox.CaretIndex;
                    TweetTextBox.Text = TweetTextBox.Text.Insert(index, text);
                    TweetTextBox.CaretIndex = index + text.Length;
                }
                else
                {
                    int index = TweetTextBox.SelectionStart;
                    TweetTextBox.Text = TweetTextBox.Text.Remove(index, TweetTextBox.SelectionLength).Insert(index, text);
                    TweetTextBox.CaretIndex = index + text.Length;
                }

                if (schemaRegex.IsMatch(text) && (text.Length > 50 || text.IndexOfAny(new[] { '!', '?' }) != -1 || TweetTextBox.Text.Length > 140))
                {
                    StatusText = Resource_jp.URL_converted_to_short;
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        var url = (string)state;

                        var shorten = BitlyHelper.ConvertTo(url);

                        this.Invoke(targetUrl =>
                        {
                            var index = TweetTextBox.Text.IndexOf(url);
                            TweetTextBox.Text = TweetTextBox.Text.Replace(url, targetUrl);
                            TweetTextBox.CaretIndex = index + targetUrl.Length;
                            StatusText = Resource_jp.success_shorten_url_msg;
                        }, shorten);
                    }, text);
                }
            }
        }

        private void QuickSearchTimer_Tick(object sender, EventArgs e)
        {
            QuickSearch();
            _quickSearchTimer.Stop();
        }

        private void QuickSearch()
        {
            Timelines.SearchAll(searchTermTextBox.Text);
        }

        private void SearchTermTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            _quickSearchTimer.Stop();
            _quickSearchTimer.Start();
        }

        private void SearchCancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (searchTermTextBox.Text.Length != 0)
            {
                searchTermTextBox.Clear();
                QuickSearch();
            }

            searchTermTextBox.Focus();
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _isClosing = true;
            Close();
        }

        private void ApportionCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var timeline = (Timeline)e.Parameter;

            var item = (ITwitterItem)GetSelectedItem();

            if (timeline.Filters.Any(p => p.Pattern == item.Sender.ScreenName && p.Type == FilterType.Name))
            {
                return;
            }

            timeline.Filters.Add(new Filter { Pattern = item.Sender.ScreenName, Type = FilterType.Name });

            timeline.Update(Timelines.TypeAt(TimelineType.Recent).Items.Concat(Timelines.TypeAt(TimelineType.Replies).Items));
        }

        private void FooterCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var footer = (string)e.Parameter;

            if (footer == Settings.Default.TweetFooter)
            {
                Settings.Default.EnableTweetFooter = !Settings.Default.EnableTweetFooter;
            }
            else
            {
                Settings.Default.EnableTweetFooter = true;
            }

            Settings.Default.TweetFooter = footer;
            client.Footer = Settings.Default.EnableTweetFooter ? Settings.Default.TweetFooter : string.Empty;

            UpdateFooterMenu();
        }

        private void UpdateFooterMenu()
        {
            var contextMenu = (ContextMenu)FindResource("TextBoxContextMenu");
            var footerMenuItem = (MenuItem)contextMenu.Items[11];

            foreach (var item in Settings.Default.TweetFooterHistory)
            {
                var menuItem = (MenuItem)footerMenuItem.ItemContainerGenerator.ContainerFromItem(item);

                if (menuItem == null)
                {
                    continue;
                }

                menuItem.GetBindingExpression(MenuItem.IsCheckedProperty).UpdateTarget();
            }
        }

        private void SearchButton_Checked(object sender, RoutedEventArgs e)
        {
            this.AsyncInvoke(() =>
                {
                    searchTermTextBox.Focus();
                });
        }

        private void SearchButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (searchTermTextBox.Text.Length != 0)
            {
                searchTermTextBox.Clear();
                QuickSearch();
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SearchCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            searchButton.IsChecked = searchButton.IsChecked.Value ? false : true;
        }

        private void TimelineTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainViewer == null)
            {
                return;
            }

            if (e.RemovedItems.Count != 0 && (e.RemovedItems[0] is Timeline))
            {
                ((Timeline)e.RemovedItems[0]).VerticalOffset = _mainViewer.VerticalOffset;
            }

            if (e.AddedItems.Count != 0 && (e.AddedItems[0] is Timeline))
            {
                _mainViewer.ScrollToVerticalOffset(((Timeline)e.AddedItems[0]).VerticalOffset);
            }
        }

        private void TimelineListBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
        }

        private bool _isSilentMode = false;

        private void SilentMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            _isSilentMode = true;
        }

        private void SilentMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            _isSilentMode = false;
        }

        private TwitpicUploader _uploader = new TwitpicUploader();

        private void TwitpicCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Multiselect = false };

            if (dialog.ShowDialog() ?? false)
            {
                _uploader.UploadAsync(dialog.FileName, client.GetOAuthToken(new Uri("https://api.twitter.com/1/account/verify_credentials.json")), "");
                StatusText = Resource_jp.twitpic_to_upload;
            }
        }

        private void Uploader_UploadCompleted(object sender, TwitpicUploadCompletedEventArgs e)
        {
            this.Invoke(url =>
            {
                StatusText = Resource_jp.success_twitpic_upload_msg;

                TweetTextBox.Text = TweetTextBox.Text.Insert(TweetTextBox.CaretIndex, url);
                TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            }, e.MediaUrl);
        }

        private void PlayTitleCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var iTunes = new iTunesLib.iTunesAppClass();

            try
            {
                var track = iTunes.CurrentTrack;

                if (track == null)
                {
                    return;
                }

                var text = string.Format("♪{0} - {1}({2})", track.Name, track.Album, track.Artist);

                TweetTextBox.Text = TweetTextBox.Text.Insert(TweetTextBox.CaretIndex, text);
                TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(iTunes);

                iTunes = null;
            }
        }

        private void PlayTitleCommand_CanExecuted(object sender, CanExecuteRoutedEventArgs e)
        {
            var processes = Process.GetProcessesByName("iTunes");

            if (processes.Length == 0)
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = true;
        }

        private ListBox _listBox;

        private void InReplyToCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var status = (Status)(e.Parameter ?? GetSelectedItem());

            if (status == null || status.InReplyToStatusId == 0)
            {
                return;
            }

            var currentTimeline = (Timeline)TimelineTabControl.SelectedItem;

            var replyTo = currentTimeline.Items.FirstOrDefault(p => p.Id == status.InReplyToStatusId);

            if (replyTo == null)
            {
                var timeline = Timelines.TypeAt(TimelineType.Recent);

                replyTo = timeline.Items.FirstOrDefault(p => p.Id == status.InReplyToStatusId);

                if (replyTo == null)
                {
                    replyTo = client.GetStatus(status.InReplyToStatusId);

                    if (replyTo == null)
                    {
                        return;
                    }
                }

                if (currentTimeline.Type == TimelineType.List || currentTimeline.Type == TimelineType.Search)
                {
                    currentTimeline.Update(new[] { replyTo });
                }
                else
                {
                    currentTimeline = timeline;

                    Timelines.Update(new[] { replyTo });
                }
            }

            if (_listBox == null)
            {
                Func<DependencyObject, ListBox> getChildVisual = null;
                getChildVisual = dobj =>
                {
                    if (dobj is ListBox) return dobj as ListBox;
                    int count = VisualTreeHelper.GetChildrenCount(dobj);
                    for (int i = 0; i < count; i++)
                    {
                        var ret = getChildVisual(VisualTreeHelper.GetChild(dobj, i));
                        if (ret != null) return ret;
                    }
                    return null;
                };
                _listBox = getChildVisual(TimelineTabControl);
            }

            if (_listBox == null) return;

            currentTimeline.View.MoveCurrentTo(replyTo);

            if (currentTimeline.Type != TimelineType.List && currentTimeline.Type != TimelineType.Search)
            {
                TimelineTabControl.SelectedItem = Timelines.TypeAt(TimelineType.Recent);
            }

            this.AsyncInvoke(p => _listBox.ScrollIntoView(p), replyTo, DispatcherPriority.Background);

            ForceActivate(false);
        }

        private const string API_KEY = "ABQIAAAAM4XZ7vZN42wOxOVhc81rGxQTX-F87mHHbR08XYRqFXjuOsafMxRVg7VuNiNB-o8yiyimIF-9mXPghQ";

        private double? _latitude;
        private double? _longitude;

        private void GpsLocationCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_geoWatcher.TryStart(false, TimeSpan.FromMilliseconds(1000)))
            {
                return;
            }

            var position = _geoWatcher.Position;

            var xmlns = XNamespace.Get("http://earth.google.com/kml/2.0");
            var oasis = XNamespace.Get("urn:oasis:names:tc:ciq:xsdschema:xAL:2.0");

            var document = XDocument.Load(string.Format("http://maps.google.com/maps/geo?ll={0},{1}&output=xml&key={2}&hl=ja&oe=UTF8",
                position.Location.Latitude, position.Location.Longitude, API_KEY));

            _latitude = position.Location.Latitude;
            _longitude = position.Location.Longitude;

            var placemarks = (from p in document.Descendants(xmlns + "Placemark")
                              select new
                              {
                                  Address = p.Element(xmlns + "address").Value,
                                  Accuracy = int.Parse(p.Element(oasis + "AddressDetails").Attribute("Accuracy").Value)
                              }).ToList();

            var placemark = placemarks.Where(p => p.Accuracy < 5 || p.Accuracy == 8).OrderByDescending(p => p.Accuracy).FirstOrDefault();

            if (placemark != null)
            {
                var text = "L:" + placemark.Address.Substring(4);

                TweetTextBox.Text = TweetTextBox.Text.Insert(TweetTextBox.CaretIndex, text);
                TweetTextBox.CaretIndex = TweetTextBox.Text.Length;
            }
        }

        private void GpsLocationCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _geoWatcher.Status == GeoPositionStatus.Ready || _geoWatcher.Status == GeoPositionStatus.Initializing;
        }

        private void FollowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                if (!client.CreateFollow((string)state))
                {
                    this.Invoke(() => StatusText = "フォローに失敗しました。");
                }
            }, ((Status)e.Parameter).Sender.ScreenName);
        }

        private void UnfollowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var user = (User)state;
                if (client.DeleteFollow(user.ScreenName))
                {
                    this.Invoke(() => Timelines.RemoveAll(p => p.Sender.Id == user.Id));
                }
                else
                {
                    this.Invoke(() => StatusText = Resource_jp.failed_to_release_follow_up);
                }
            }, ((Status)e.Parameter).Sender);
        }

        private void BlockCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var user = (User)state;
                if (client.CreateBlock(user.ScreenName))
                {
                    this.Invoke(() => Timelines.RemoveAll(p => p.Sender.Id == user.Id));
                }
                else
                {
                    this.Invoke(() => StatusText = Resource_jp.failed_to_block);
                }
            }, ((Status)e.Parameter).Sender);
        }

        private void HashtagCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var value = e.Parameter as string;

            if (value != null)
            {
                var timeline = new Timeline
                {
                    Name = value,
                    Type = TimelineType.Search,
                    Tag = value,
                };

                Timelines.Add(timeline);

                ThreadPool.QueueUserWorkItem(state =>
                {
                    this.Invoke(p => timeline.Update(p), client.Search(timeline.Tag, timeline.SinceId));
                });

                TimelineTabControl.SelectedItem = timeline;
            }
        }

        private void TimelineListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
        }
    }
}