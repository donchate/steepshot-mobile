﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using Steepshot.Core.Models.Requests;
using Steepshot.Core.Presenters;
using Steepshot.Core.Utils;
using Steepshot.iOS.Cells;
using Steepshot.iOS.ViewControllers;
using Steepshot.iOS.ViewSources;
using UIKit;

namespace Steepshot.iOS.Views
{
    public partial class FollowViewController : BaseViewControllerWithPresenter<FollowersPresenter>
    {
        private FollowTableViewSource _tableSource;
        public string Username = BasePresenter.User.Login;
        public FriendsType FriendsType = FriendsType.Followers;

        protected override void CreatePresenter()
        {
            _presenter = new FollowersPresenter();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            _tableSource = new FollowTableViewSource();
            _tableSource.TableItems = _presenter.Users;
            followTableView.Source = _tableSource;
            followTableView.LayoutMargins = UIEdgeInsets.Zero;
            followTableView.RegisterClassForCellReuse(typeof(FollowViewCell), nameof(FollowViewCell));
            followTableView.RegisterNibForCellReuse(UINib.FromName(nameof(FollowViewCell), NSBundle.MainBundle), nameof(FollowViewCell));

            _tableSource.Follow += (vote, url, action) =>
            {
                Follow(vote, url, action);
            };

            _tableSource.ScrolledToBottom += () =>
            {
                if (_presenter._hasItems)
                    GetItems();
            };

            _tableSource.GoToProfile += (username) =>
            {
                var myViewController = new ProfileViewController();
                myViewController.Username = username;
                NavigationController.PushViewController(myViewController, true);
            };

            GetItems();
        }

        public override void ViewWillAppear(bool animated)
        {
            NavigationController.SetNavigationBarHidden(false, true);
            base.ViewWillAppear(animated);
        }

        public override void ViewWillDisappear(bool animated)
        {
            if (Username == BasePresenter.User.Login)
                NavigationController.SetNavigationBarHidden(true, true);
            base.ViewWillDisappear(animated);
        }

        public async Task GetItems()
        {
            if (progressBar.IsAnimating)
                return;

            progressBar.StartAnimating();
            await _presenter.GetItems(FriendsType, Username).ContinueWith((errors) =>
            {
                var errorsList = errors.Result;
                if (errorsList != null && errorsList.Count > 0)
                    ShowAlert(errorsList[0]);
                InvokeOnMainThread(() =>
                {
                    followTableView.ReloadData();
                    progressBar.StopAnimating();
                });
            });
        }


        public async Task Follow(FollowType followType, string author, Action<string, bool?> callback)
        {
            bool? success = null;
            try
            {
                var request = new FollowRequest(BasePresenter.User.UserInfo, followType, author);
                var response = await _presenter.Follow(_presenter.Users.First(fgh => fgh.Author == author));
                if (response.Success)
                {
                    var user = _tableSource.TableItems.FirstOrDefault(f => f.Author == request.Username);
                    if (user != null)
                        success = user.HasFollowed = response.Result.IsSuccess;
                }
                //else
                //Reporter.SendCrash(Localization.Errors.FollowError + response.Errors[0], BasePresenter.User.Login, AppVersion);

            }
            catch (Exception ex)
            {
                AppSettings.Reporter.SendCrash(ex);
            }
            finally
            {
                callback(author, success);
            }
        }
    }
}