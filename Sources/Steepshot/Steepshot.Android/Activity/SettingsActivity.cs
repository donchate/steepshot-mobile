﻿using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Com.OneSignal;
using CheeseBind;
using Steepshot.Base;
using Steepshot.Core;
using Steepshot.Core.Extensions;
using Steepshot.Core.Localization;
using Steepshot.Core.Models.Enums;
using Steepshot.Core.Presenters;
using Steepshot.Core.Utils;
using Steepshot.Utils;
using Steepshot.Core.Models.Requests;
using System.Threading.Tasks;
using Steepshot.Core.Authorization;
using System.Collections.Generic;
using Android.Graphics;
using Android.Support.Design.Widget;

namespace Steepshot.Activity
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public sealed class SettingsActivity : BaseActivityWithPresenter<UserProfilePresenter>
    {
        private PushSettings PushSettings;
        private BottomSheetDialog _propertiesActionsDialog;
        private bool _lowRatedChanged;
        private bool _nsfwChanged;
        private UserInfo _currentUser;

#pragma warning disable 0649, 4014
        [BindView(Resource.Id.dtn_terms_of_service)] private Button _termsButton;
        [BindView(Resource.Id.tests)] private AppCompatButton _testsButton;
        [BindView(Resource.Id.btn_guide)] private Button _guideButton;
        [BindView(Resource.Id.nsfw_switch)] private SwitchCompat _nsfwSwitcher;
        [BindView(Resource.Id.low_switch)] private SwitchCompat _lowRatedSwitcher;
        [BindView(Resource.Id.version_textview)] private TextView _versionText;
        [BindView(Resource.Id.nsfw_switch_text)] private TextView _nsfwSwitchText;
        [BindView(Resource.Id.low_switch_text)] private TextView _lowSwitchText;
        [BindView(Resource.Id.profile_login)] private TextView _viewTitle;
        [BindView(Resource.Id.btn_switcher)] private ImageButton _switcher;
        [BindView(Resource.Id.btn_settings)] private ImageButton _settings;
        [BindView(Resource.Id.btn_back)] private ImageButton _backButton;
        [BindView(Resource.Id.power_switch)] private SwitchCompat _powerSwitch;
        [BindView(Resource.Id.power_switch_text)] private TextView _powerSwitchText;
        [BindView(Resource.Id.power_hint)] private TextView _powerHint;
        [BindView(Resource.Id.header_text)] private TextView _notificationSettings;
        [BindView(Resource.Id.post_upvotes)] private TextView _notificationUpvotes;
        [BindView(Resource.Id.post_upvotes_switch)] private SwitchCompat _notificationUpvotesSwitch;
        [BindView(Resource.Id.comments_upvotes)] private TextView _notificationCommentsUpvotes;
        [BindView(Resource.Id.comments_upvotes_switch)] private SwitchCompat _notificationCommentsUpvotesSwitch;
        [BindView(Resource.Id.following)] private TextView _notificationFollowing;
        [BindView(Resource.Id.following_switch)] private SwitchCompat _notificationFollowingSwitch;
        [BindView(Resource.Id.comments)] private TextView _notificationComments;
        [BindView(Resource.Id.comments_switch)] private SwitchCompat _notificationCommentsSwitch;
        [BindView(Resource.Id.posting)] private TextView _notificationPosting;
        [BindView(Resource.Id.posting_switch)] private SwitchCompat _notificationPostingSwitch;
        [BindView(Resource.Id.transfer)] private TextView _notificationTransfer;
        [BindView(Resource.Id.transfer_switch)] private SwitchCompat _notificationTransferSwitch;

        [BindView(Resource.Id.steem_avatar)] private ImageView _steemAvatar;
        [BindView(Resource.Id.steem_title)] private TextView _steemTitle;
        [BindView(Resource.Id.steem_logo)] private ImageView _steemLogo;
        [BindView(Resource.Id.steem_state)] private ImageView _steemState;
        [BindView(Resource.Id.golos_avatar)] private ImageView _golosAvatar;
        [BindView(Resource.Id.golos_title)] private TextView _golosTitle;
        [BindView(Resource.Id.golos_logo)] private ImageView _golosLogo;
        [BindView(Resource.Id.golos_state)] private ImageView _golosState;

        [BindView(Resource.Id.steem_account)] private LinearLayout _steemLyt;
        [BindView(Resource.Id.golos_account)] private LinearLayout _golosLyt;
        [BindView(Resource.Id.steem_button)] private Button _steemConnectButton;
        [BindView(Resource.Id.golos_button)] private Button _golosConnectButton;
        [BindView(Resource.Id.steem_button_lyt)] private RelativeLayout _steemConnectLyt;
        [BindView(Resource.Id.golos_button_lyt)] private RelativeLayout _golosConnectLyt;
        [BindView(Resource.Id.steem_spinner)] private ProgressBar _steemLoader;
        [BindView(Resource.Id.golos_spinner)] private ProgressBar _golosLoader;

#pragma warning restore 0649

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.lyt_settings);
            Cheeseknife.Bind(this);

            var appInfoService = AppSettings.AppInfo;
            var accounts = AppSettings.User.GetAllAccounts();
            _currentUser = AppSettings.User.UserInfo;

            _propertiesActionsDialog = new BottomSheetDialog(this);
            _propertiesActionsDialog.Window.RequestFeature(WindowFeatures.NoTitle);

            _viewTitle.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.AppSettingsTitle);
            _nsfwSwitchText.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.ShowNsfw);
            _lowSwitchText.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.ShowLowRated);
            _versionText.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.AppVersion, appInfoService.GetAppVersion(), appInfoService.GetBuildVersion());
            _guideButton.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Guidelines);
            _termsButton.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.ToS);
            _powerSwitchText.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.VotingPowerSetting);
            _powerHint.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.PowerHint);
            _notificationSettings.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationSettings);
            _notificationUpvotes.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationPostUpvotes);
            _notificationCommentsUpvotes.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationCommentsUpvotes);
            _notificationFollowing.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationFollow);
            _notificationComments.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationComment);
            _notificationPosting.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.NotificationPosting);
            _notificationTransfer.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.RecievedTransfers);
            _steemConnectButton.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Connect);
            _golosConnectButton.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Connect);

            SetupAccounts(accounts);

            _backButton.Visibility = ViewStates.Visible;
            _backButton.Click += GoBackClick;
            _switcher.Visibility = ViewStates.Gone;
            _settings.Visibility = ViewStates.Gone;

            _steemTitle.Typeface = Style.Semibold;
            _golosTitle.Typeface = Style.Semibold;
            _steemConnectButton.Typeface = Style.Semibold;
            _golosConnectButton.Typeface = Style.Semibold;
            _viewTitle.Typeface = Style.Semibold;
            _versionText.Typeface = Style.Regular;
            _nsfwSwitchText.Typeface = Style.Semibold;
            _lowSwitchText.Typeface = Style.Semibold;
            _termsButton.Typeface = Style.Semibold;
            _powerSwitchText.Typeface = Style.Semibold;
            _powerHint.Typeface = Style.Light;
            _notificationSettings.Typeface = Style.Semibold;
            _notificationUpvotes.Typeface = Style.Semibold;
            _notificationCommentsUpvotes.Typeface = Style.Semibold;
            _notificationFollowing.Typeface = Style.Semibold;
            _notificationComments.Typeface = Style.Semibold;
            _notificationPosting.Typeface = Style.Semibold;
            _notificationTransfer.Typeface = Style.Semibold;
            _termsButton.Click += TermsOfServiceClick;
            _guideButton.Typeface = Style.Semibold;
            _guideButton.Click += GuideClick;

            _steemConnectButton.Click += (sender, e) =>
            {
                _steemLoader.Visibility = ViewStates.Visible;
                _steemConnectButton.Text = string.Empty;
                _steemConnectButton.Enabled = false;
                OnAccountAdd();
            };

            _golosConnectButton.Click += (sender, e) =>
            {
                _golosLoader.Visibility = ViewStates.Visible;
                _golosConnectButton.Text = string.Empty;
                _golosConnectButton.Enabled = false;
                OnAccountAdd();
            };

            _steemLyt.Click += (sender, e) =>
            {
                OpenAccountProperties(accounts.FirstOrDefault(p => p.Chain.Equals(KnownChains.Steem)));
            };

            _golosLyt.Click += (sender, e) =>
            {
                OpenAccountProperties(accounts.FirstOrDefault(p => p.Chain.Equals(KnownChains.Golos)));
            };

            _nsfwSwitcher.Checked = _currentUser.IsNsfw;
            _lowRatedSwitcher.Checked = _currentUser.IsLowRated;
            _powerSwitch.Checked = _currentUser.ShowVotingSlider;


            Presenter.SubscriptionsUpdated += _presenter_SubscriptionsUpdated;
            Presenter.TryCheckSubscriptions();

            PushSettings = _currentUser.PushSettings;
            EnableNotificationSwitch(false);

            _nsfwSwitcher.CheckedChange += OnNsfwSwitcherOnCheckedChange;
            _lowRatedSwitcher.CheckedChange += OnLowRatedSwitcherOnCheckedChange;
            _powerSwitch.CheckedChange += PowerSwitchOnCheckedChange;

            //for tests
            if (_currentUser.IsDev || _currentUser.Login.Equals("joseph.kalu"))
            {
                _testsButton.Visibility = ViewStates.Visible;
                _testsButton.Click += StartTestActivity;
            }
        }

        private void _presenter_SubscriptionsUpdated()
        {
            _notificationUpvotesSwitch.Checked = PushSettings.HasFlag(PushSettings.Upvote);
            _notificationCommentsUpvotesSwitch.Checked = PushSettings.HasFlag(PushSettings.UpvoteComment);
            _notificationFollowingSwitch.Checked = PushSettings.HasFlag(PushSettings.Follow);
            _notificationCommentsSwitch.Checked = PushSettings.HasFlag(PushSettings.Comment);
            _notificationPostingSwitch.Checked = PushSettings.HasFlag(PushSettings.User);
            _notificationTransferSwitch.Checked = PushSettings.HasFlag(PushSettings.Transfer);

            _notificationUpvotesSwitch.CheckedChange += NotificationChange;
            _notificationCommentsUpvotesSwitch.CheckedChange += NotificationChange;
            _notificationFollowingSwitch.CheckedChange += NotificationChange;
            _notificationCommentsSwitch.CheckedChange += NotificationChange;
            _notificationPostingSwitch.CheckedChange += NotificationChange;
            _notificationTransferSwitch.CheckedChange += NotificationChange;

            EnableNotificationSwitch(true);
        }

        private void EnableNotificationSwitch(bool isenabled)
        {
            _notificationUpvotesSwitch.Enabled = isenabled;
            _notificationCommentsUpvotesSwitch.Enabled = isenabled;
            _notificationFollowingSwitch.Enabled = isenabled;
            _notificationCommentsSwitch.Enabled = isenabled;
            _notificationPostingSwitch.Enabled = isenabled;
            _notificationTransferSwitch.Enabled = isenabled;
        }

        private void PowerSwitchOnCheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            AppSettings.User.ShowVotingSlider = e.IsChecked;
        }

        protected override void OnResume()
        {
            _steemLoader.Visibility = _golosLoader.Visibility = ViewStates.Gone;
            _steemConnectButton.Text = _golosConnectButton.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Connect);
            _steemConnectButton.Enabled = _golosConnectButton.Enabled = true;
            _steemLoader.Visibility = _golosLoader.Visibility = ViewStates.Gone;

            base.OnResume();
        }

        public override void OnBackPressed()
        {
            if (_nsfwChanged || _lowRatedChanged)
                AppSettings.ProfileUpdateType = ProfileUpdateType.Full;
            base.OnBackPressed();
        }

        protected override async void OnDestroy()
        {
            base.OnDestroy();
            await SavePushSettings();
            Cheeseknife.Reset(this);
        }

        private void OnLowRatedSwitcherOnCheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            AppSettings.User.IsLowRated = _lowRatedSwitcher.Checked;
            _lowRatedChanged = !_lowRatedChanged;
        }

        private void OnNsfwSwitcherOnCheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            AppSettings.User.IsNsfw = _nsfwSwitcher.Checked;
            _nsfwChanged = !_nsfwChanged;
        }

        private void NotificationChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (!(sender is SwitchCompat))
                return;

            var subscription = PushSettings.None;

            if (Equals(sender, _notificationUpvotesSwitch))
                subscription = PushSettings.Upvote;
            else if (Equals(sender, _notificationCommentsUpvotesSwitch))
                subscription = PushSettings.UpvoteComment;
            else if (Equals(sender, _notificationFollowingSwitch))
                subscription = PushSettings.Follow;
            else if (Equals(sender, _notificationCommentsSwitch))
                subscription = PushSettings.Comment;
            else if (Equals(sender, _notificationPostingSwitch))
                subscription = PushSettings.User;
            else if (Equals(sender, _notificationTransferSwitch))
                subscription = PushSettings.Transfer;

            if (e.IsChecked)
                PushSettings |= subscription;
            else
                PushSettings ^= subscription;
        }

        private async Task SavePushSettings()
        {
            if (_currentUser.PushSettings == PushSettings)
                return;

            var model = new PushNotificationsModel(_currentUser, true)
            {
                Subscriptions = PushSettings.FlagToStringList()
            };

            var resp = await Presenter.TrySubscribeForPushes(model);
            if (resp.IsSuccess)
            {
                _currentUser.PushSettings = PushSettings;
                AppSettings.DataProvider.Update(_currentUser);
            }
            else
                this.ShowAlert(resp.Exception);
        }

        private void OnAdapterPickAccount(UserInfo userInfo)
        {
            if (userInfo == null)
                return;

            SwitchChain(userInfo);
        }

        private void OnAdapterDeleteAccount(UserInfo userInfo)
        {
            if (userInfo == null)
                return;

            OneSignal.Current.DeleteTag("username");
            OneSignal.Current.DeleteTag("player_id");
            OneSignal.Current.ClearAndroidOneSignalNotifications();
            var chainToDelete = userInfo.Chain;
            AppSettings.User.Delete(userInfo);
            RemoveChain(chainToDelete);
        }

        private void OpenAccountProperties(UserInfo account)
        {
            var inflater = (LayoutInflater)GetSystemService(LayoutInflaterService);
            using (var dialogView = inflater.Inflate(Resource.Layout.lyt_account_popup, null))
            {
                var logout = dialogView.FindViewById<Button>(Resource.Id.logout);
                logout.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Logout);
                logout.Typeface = Style.Semibold;

                var switchAccount = dialogView.FindViewById<Button>(Resource.Id.switch_account);
                switchAccount.Text = $"{AppSettings.LocalizationManager.GetText(LocalizationKeys.SwitchTo)} {account.Login}";
                switchAccount.Typeface = Style.Semibold;

                if (account.Chain != AppSettings.User.Chain)
                {
                    switchAccount.Visibility = ViewStates.Visible;
                }

                var cancel = dialogView.FindViewById<Button>(Resource.Id.cancel);
                cancel.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.Cancel);
                cancel.Typeface = Style.Semibold;

                logout.Click += (sender, e) =>
                {
                    OnAdapterDeleteAccount(account);
                    _propertiesActionsDialog.Dismiss();
                };

                switchAccount.Click += (sender, e) =>
                {
                    OnAdapterPickAccount(account);
                    _propertiesActionsDialog.Dismiss();
                };

                cancel.Click += (sender, e) =>
                {
                    _propertiesActionsDialog.Dismiss();
                };

                _propertiesActionsDialog.SetContentView(dialogView);
                dialogView.SetBackgroundColor(Color.Transparent);
                _propertiesActionsDialog.Window.FindViewById(Resource.Id.design_bottom_sheet).SetBackgroundColor(Color.Transparent);
                _propertiesActionsDialog.Show();
            }
        }

        private void GoBackClick(object sender, EventArgs e)
        {
            OnBackPressed();
        }

        private void TermsOfServiceClick(object sender, EventArgs e)
        {
            var uri = Android.Net.Uri.Parse(Constants.Tos);
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
        }

        private void GuideClick(object sender, EventArgs e)
        {
            var uri = Android.Net.Uri.Parse(Constants.Guide);
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
        }

        private void OnAccountAdd()
        {
            App.MainChain = App.MainChain == KnownChains.Steem ? KnownChains.Golos : KnownChains.Steem;
            var intent = new Intent(this, typeof(PreSignInActivity));
            StartActivity(intent);
        }

        private void StartTestActivity(object sender, EventArgs e)
        {
            var intent = new Intent(this, typeof(TestActivity));
            StartActivity(intent);
        }

        private void SwitchChain(UserInfo user)
        {
            if (App.MainChain != user.Chain)
            {
                App.MainChain = user.Chain;
                AppSettings.User.SwitchUser(user);

                var i = new Intent(ApplicationContext, typeof(RootActivity));
                i.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                StartActivity(i);
            }
        }

        private void RemoveChain(KnownChains chain)
        {
            var accounts = AppSettings.User.GetAllAccounts();
            if (accounts.Count == 0)
            {
                var i = new Intent(ApplicationContext, typeof(GuestActivity));
                i.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                StartActivity(i);
                Finish();
            }
            else
            {
                if (App.MainChain == chain)
                {
                    var user = accounts.First();
                    App.MainChain = user.Chain;
                    AppSettings.User.SwitchUser(user);

                    var i = new Intent(ApplicationContext, typeof(RootActivity));
                    i.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                    StartActivity(i);
                    Finish();
                }
                else
                {
                    SetupAccounts(accounts);
                }
            }
        }

        private void SetupAccounts(List<UserInfo> accounts)
        {
            _golosLyt.Enabled = false;
            _steemLyt.Enabled = false;

            //_steemAvatar.Visibility = _golosAvatar.Visibility = ViewStates.Gone;
            _steemLogo.Visibility = _golosLogo.Visibility = ViewStates.Gone;
            _steemState.Visibility = _golosState.Visibility = ViewStates.Gone;

            _steemConnectButton.Visibility = _golosConnectButton.Visibility = ViewStates.Visible;
            _steemConnectLyt.Visibility = _golosConnectLyt.Visibility = ViewStates.Visible;

            _steemTitle.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.SteemitAccount);
            _golosTitle.Text = AppSettings.LocalizationManager.GetText(LocalizationKeys.GolosAccount);

            _steemTitle.SetTextColor(Color.Black);
            _golosTitle.SetTextColor(Color.Black);

            foreach (var account in accounts)
            {
                switch (account.Chain)
                {
                    case KnownChains.Steem:
                        _steemLyt.Enabled = true;

                        //_steemAvatar.Visibility = ViewStates.Visible;
                        _steemLogo.Visibility = ViewStates.Visible;
                        _steemState.Visibility = ViewStates.Visible;
                        _steemConnectLyt.Visibility = ViewStates.Gone;

                        _steemTitle.Text = account.Login;

                        if (account.Chain == AppSettings.User.Chain)
                        {
                            _steemTitle.SetTextColor(Style.R255G34B5);
                            _steemState.SetImageResource(Resource.Drawable.ic_checked);
                        }
                        else
                        {
                            _steemState.SetImageResource(Resource.Drawable.ic_unchecked);
                        }
                        break;
                    case KnownChains.Golos:
                        _golosLyt.Enabled = true;

                        //_golosAvatar.Visibility = ViewStates.Visible;
                        _golosLogo.Visibility = ViewStates.Visible;
                        _golosState.Visibility = ViewStates.Visible;
                        _golosConnectLyt.Visibility = ViewStates.Gone;

                        _golosTitle.Text = account.Login;
                        if (account.Chain == AppSettings.User.Chain)
                        {
                            _golosTitle.SetTextColor(Style.R255G34B5);
                            _golosState.SetImageResource(Resource.Drawable.ic_checked);
                        }
                        else
                        {
                            _golosState.SetImageResource(Resource.Drawable.ic_unchecked);
                        }
                        break;
                }
            }
        }
    }
}
