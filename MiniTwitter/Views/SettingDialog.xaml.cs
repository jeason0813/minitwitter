﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Deployment.Application;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MiniTwitter.Controls;
using MiniTwitter.Extensions;
using MiniTwitter.Models.Input;
using MiniTwitter.Properties;
using MiniTwitter.Resources.languages.jp;
using KeyBinding = MiniTwitter.Models.Input.KeyBinding;

namespace MiniTwitter.Views
{
    /// <summary>
    /// SettingDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingDialog : Window
    {
        public SettingDialog()
        {
            InitializeComponent();

            if (Settings.Default.IsClearTypeEnabled)
            {
                TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            }
        }

        private ObservableCollection<KeyBinding> keyBindings;
        private ObservableCollection<SoundBinding> soundBindings;
        private ObservableCollection<KeywordBinding> keywordBindings;

        private static readonly PopupLocation[] locations = new[]
        {
            PopupLocation.Auto, PopupLocation.LeftTop, PopupLocation.LeftBottom,
            PopupLocation.RightTop, PopupLocation.RightBottom
        };

        public static PopupLocation[] Locations
        {
            get { return locations; }
        }

        private static readonly KeywordAction[] keywordActions = new[]
        {
            KeywordAction.Favorite, KeywordAction.Ignore
        };

        public static KeywordAction[] KeywordActions
        {
            get { return SettingDialog.keywordActions; }
        }

        private void SettingDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 見やすくするために、変数名を短縮する
            Settings settings = Settings.Default;
            // パスワード、プロキシパスワード
            PasswordBox.Password = settings.Password;
            ProxyPasswordBox.Password = settings.ProxyPassword;
            // キーボードショートカット設定
            var array = Enum.GetValues(typeof(KeyAction));
            keyBindings = new ObservableCollection<KeyBinding>(settings.KeyBindings ?? Enumerable.Empty<KeyBinding>());
            foreach (KeyAction item in array)
            {
                if (keyBindings.SingleOrDefault(p => p.Action == item) == null)
                {
                    keyBindings.Add(new KeyBinding { Action = item });
                }
            }
            keyBindings.BeginEdit();
            KeyMappingComboBox.SelectedItem = KeyMapping.KeyMappings.SingleOrDefault(p => p.Key == settings.KeyMapping);
            CommandComboBox.ItemsSource = keyBindings;
            // サウンド設定
            soundBindings = new ObservableCollection<SoundBinding>(settings.SoundBindings);
            soundBindings.BeginEdit();
            SoundListView.ItemsSource = soundBindings;
            // キーワード設定
            keywordBindings = new ObservableCollection<KeywordBinding>(settings.KeywordBindings ?? Enumerable.Empty<KeywordBinding>());
            keywordBindings.BeginEdit();
            KeywordListView.ItemsSource = keywordBindings;
            //// カラー設定
            //colorSchemes = new ObservableCollection<ColorScheme>(settings.ColorSchemes ?? Enumerable.Empty<ColorScheme>());
            //colorSchemes.BeginEdit();
            //ColorListView.ItemsSource = colorSchemes;
            // メッセージフッタ履歴
            TweetFooterComboBox.ItemsSource = settings.TweetFooterHistory;
            BindingGroup.BeginEdit();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 変更をコミットする
            BindingGroup.CommitEdit();
            // 見やすくするために変数名を短縮する
            Settings settings = Settings.Default;
            // パスワードを保存
            settings.Password = PasswordBox.Password;
            settings.ProxyPassword = ProxyPasswordBox.Password;
            // キーボードショートカットを保存
            if (KeyMappingComboBox.SelectedValue != null)
            {
                settings.KeyMapping = ((KeyMapping)KeyMappingComboBox.SelectedValue).Name;
            }
            settings.KeyBindings.Clear();
            foreach (var item in keyBindings.Where(p => p.Key != Key.None))
            {
                settings.KeyBindings.Add(item);
            }
            // サウンド設定を保存
            settings.SoundBindings.Clear();
            foreach (var item in soundBindings)
            {
                settings.SoundBindings.Add(item);
            }
            // キーワード設定を保存
            settings.KeywordBindings.Clear();
            foreach (var item in keywordBindings)
            {
                settings.KeywordBindings.Add(item);
            }
            // メッセージフッタの履歴を保存
            if (!settings.TweetFooter.IsNullOrEmpty() && !settings.TweetFooterHistory.Contains(settings.TweetFooter))
            {
                settings.TweetFooterHistory.Add(settings.TweetFooter);
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 変更をキャンセルする
            keyBindings.CancelEdit();
            soundBindings.CancelEdit();
            keywordBindings.CancelEdit();
            DialogResult = false;
        }

        private void AssignKeyButton_Click(object sender, RoutedEventArgs e)
        {
            ShortcutKeyBox.GetBindingExpression(ShortcutKeyBox.KeyProperty).UpdateSource();
            ShortcutKeyBox.GetBindingExpression(ShortcutKeyBox.ModifierKeysProperty).UpdateSource();
        }

        private void DeleteKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var binding = (KeyBinding)CommandComboBox.SelectedItem;
            binding.Key = Key.None;
            binding.ModifierKeys = ModifierKeys.None;
        }

        private void KeyMappingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || e.AddedItems.Count == 0)
            {
                return;
            }
            var keyMapping = (KeyMapping)KeyMappingComboBox.SelectedValue;
            foreach (var item in keyMapping.KeyBindings)
            {
                var keyBinding = keyBindings.SingleOrDefault(p => item.Equals(p) && p.Key == Key.None);
                if (keyBinding != null)
                {
                    keyBinding.Key = item.Key;
                    keyBinding.ModifierKeys = item.ModifierKeys;
                    keyBinding.ActionSpot = item.ActionSpot;
                }
            }
        }

        private void ResetKeyMappingButton_Click(object sender, RoutedEventArgs e)
        {
            var keyMapping = (KeyMapping)KeyMappingComboBox.SelectedValue;
            foreach (var item in keyBindings)
            {
                var binding = keyMapping.KeyBindings.SingleOrDefault(p => item.Equals(p));
                if (binding == null)
                {
                    item.Key = Key.None;
                    item.ModifierKeys = ModifierKeys.None;
                }
                else
                {
                    item.Key = binding.Key;
                    item.ModifierKeys = binding.ModifierKeys;
                }
            }
        }

        private void AddKeywordButton_Click(object sender, RoutedEventArgs e)
        {
            var keyword = KeywordTextBox.Text;
            if (keyword.IsNullOrEmpty())
            {
                return;
            }
            var binding = new KeywordBinding { Action = KeywordAction.Favorite, IsEnabled = true, Keyword = keyword };
            binding.BeginEdit();
            keywordBindings.Add(binding);
            KeywordTextBox.Clear();
        }

        private void DeleteKeywordButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (KeywordBinding)KeywordListView.SelectedItem;
            if (item == null)
            {
                return;
            }
            keywordBindings.Remove(item);
        }

        private void PlaySoundButton_Click(object sender, RoutedEventArgs e)
        {
            var sound = (SoundBinding)SoundListView.SelectedItem;
            if (!sound.FileName.IsNullOrEmpty())
            {
                try
                {
                    new SoundPlayer(sound.FileName).Play();
                }
                catch
                {
                    MessageBox.Show(Resource_jp.unsupported_format, App.NAME);
                }
            }
        }

        private void BrowseSoundButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = Resource_jp.sound_files,
                Multiselect = false,
            };
            if (dialog.ShowDialog() ?? false)
            {
                var sound = (SoundBinding)SoundListView.SelectedItem;
                sound.FileName = dialog.FileName;
            }
        }

        private void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
        {
            var deploy = ApplicationDeployment.CurrentDeployment;

            if (deploy.CheckForUpdate())
            {
                if (MessageBox.Show(Resource_jp.update_msg, App.NAME, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    deploy.UpdateAsync();

                    DialogResult = true;
                }
            }
            else
            {
                MessageBox.Show(Resource_jp.no_available_update, App.NAME);
            }
        }
    }
}
