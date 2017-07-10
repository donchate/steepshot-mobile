using System;
using Android.App;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Com.Lilarcor.Cheeseknife;
using Sweetshot.Library.Models.Requests;
using System.Threading.Tasks;
using Android.Content;

namespace Steepshot
{
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class FollowersActivity : BaseActivity, FollowersView
    {
		FollowersPresenter presenter;
        private FollowType _friendsType;
        private FollowersAdapter _followersAdapter;

#pragma warning disable 0649, 4014
        [InjectView(Resource.Id.loading_spinner)] private ProgressBar _bar;
        [InjectView(Resource.Id.Title)] TextView ViewTitle;
		[InjectView(Resource.Id.followers_list)] RecyclerView _followersList;
#pragma warning restore 0649

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var isFollowers = Intent.GetBooleanExtra("isFollowers", false);
			var username = Intent.GetStringExtra("username") ?? UserPrincipal.Instance.CurrentUser.Login ;
            _friendsType = isFollowers ? FollowType.Follow : FollowType.UnFollow;

            presenter.Collection.Clear();
			presenter.ViewLoad(_friendsType, username);
            SetContentView(Resource.Layout.lyt_followers);
            Cheeseknife.Inject(this);

            ViewTitle.Text = isFollowers ? GetString(Resource.String.text_followers) : GetString(Resource.String.text_following);

            _followersAdapter = new FollowersAdapter(this, presenter.Collection);
            _followersList.SetAdapter(_followersAdapter);
            _followersList.SetLayoutManager(new LinearLayoutManager(this));
			_followersList.AddOnScrollListener(new FollowersScrollListener(presenter, username, _friendsType));
            _followersAdapter.FollowAction += FollowersAdapter_FollowAction;
			_followersAdapter.UserAction += FollowersAdapter_UserAction;
        }

        public class FollowersScrollListener : RecyclerView.OnScrollListener
        {
			FollowersPresenter presenter;
			private string _username;
			private FollowType _followType;

			public FollowersScrollListener(FollowersPresenter presenter, string username, FollowType followType)
			{
				this.presenter = presenter;
				_username = username;
				_followType = followType;
			}
			int prevPos = 0;
			public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
			{
				int pos = ((LinearLayoutManager)recyclerView.GetLayoutManager()).FindLastCompletelyVisibleItemPosition();
				if (pos > prevPos && pos != prevPos)
				{
					if (pos == recyclerView.GetAdapter().ItemCount - 1)
					{
						if (pos < ((FollowersAdapter)recyclerView.GetAdapter()).ItemCount)
						{
							Task.Run(() => presenter.GetItems(_followType, _username));
							prevPos = pos;
						}
					}
				}
			}

            public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
            {

            }
        }

        async void FollowersAdapter_FollowAction(int position)
        {
			try
			{
				var response = await presenter.Follow(presenter.Collection[position]);
				if (response.Success)
				{
					presenter.Collection[position].IsFollow = !presenter.Collection[position].IsFollow;
					_followersAdapter.NotifyDataSetChanged();
				}
				else
				{
					Toast.MakeText(this, response.Errors[0], ToastLength.Short).Show();
					_followersAdapter.InverseFollow(position);
					_followersAdapter.NotifyDataSetChanged();
				}
			}
			catch (Exception ex)
			{
				Reporter.SendCrash(ex);
			}
        }

		private void FollowersAdapter_UserAction(int position)
		{
			Intent intent = new Intent(this, typeof(ProfileActivity));
			intent.PutExtra("ID", presenter.Collection[position].Author);
			this.StartActivity(intent);
		}

        [InjectOnClick(Resource.Id.btn_back)]
        public void GoBackClick(object sender, EventArgs e)
        {
            OnBackPressed();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _followersAdapter.NotifyDataSetChanged();
            presenter.Collection.CollectionChanged += CollectionChanged;
        }

        protected override void OnPause()
        {
            presenter.Collection.CollectionChanged -= CollectionChanged;
            base.OnPause();
        }

        void CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (_bar.Visibility == ViewStates.Visible)
                    _bar.Visibility = ViewStates.Gone;
                _followersAdapter.NotifyDataSetChanged();
            });
        }

        
        public void OnScrollChange(View v, int scrollX, int scrollY, int oldScrollX, int oldScrollY)
        {
            //int pos = ((LinearLayoutManager)_followersList.GetLayoutManager()).FindLastCompletelyVisibleItemPosition();
            //if (pos > _prevPos && pos != _prevPos)
            //{
            //    if (pos == _followersList.GetAdapter().ItemCount - 1)
            //    {
            //        if (pos < _followersAdapter.ItemCount)
            //        {
            //            Task.Run(() => ViewModel.GetItems(_followersAdapter.GetItem(_followersAdapter.ItemCount - 1).Author, 10, _friendsType));
            //            _prevPos = pos;
            //        }
            //    }
            //}
        }

		protected override void CreatePresenter()
		{
			presenter = new FollowersPresenter(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			Cheeseknife.Reset(this);
		}
	}
}