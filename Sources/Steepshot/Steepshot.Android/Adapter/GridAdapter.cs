﻿using System;
using System.Linq;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Square.Picasso;
using Steepshot.Core.Models.Common;
using Steepshot.Core.Presenters;
using Steepshot.Utils;

namespace Steepshot.Adapter
{
    public class GridAdapter<T> : RecyclerView.Adapter where T : BasePostPresenter
    {
        protected readonly T Presenter;
        protected readonly Context Context;
        public Action<Post> Click;
        protected readonly int CellSize;
        public override int ItemCount
        {
            get
            {
                var count = Presenter.Count;
                return count == 0 || Presenter.IsLastReaded ? count : count + 1;
            }
        }

        public GridAdapter(Context context, T presenter)
        {
            Context = context;
            Presenter = presenter;
            CellSize = Context.Resources.DisplayMetrics.WidthPixels / 3 - 2; // [x+2][1+x+1][2+x]
        }

        public override int GetItemViewType(int position)
        {
            if (Presenter.Count == position)
                return (int)ViewType.Loader;

            return (int)ViewType.Cell;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var post = Presenter[position];
            if (post == null)
                return;

            var vh = (ImageViewHolder)holder;
            vh.UpdateData(post, Context, CellSize);
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType)
            {
                case ViewType.Loader:
                    var loaderView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.loading_item, parent, false);
                    var loaderVh = new LoaderViewHolder(loaderView);
                    return loaderVh;
                default:
                    var view = new ImageView(Context);
                    view.SetScaleType(ImageView.ScaleType.CenterCrop);
                    view.LayoutParameters = new ViewGroup.LayoutParams(CellSize, CellSize);
                    return new ImageViewHolder(view, Click);
            }
        }
    }

    public class ImageViewHolder : RecyclerView.ViewHolder, ITarget
    {
        private readonly Action<Post> _click;
        private readonly ImageView _photo;
        private Post _post;
        private Context _context;
        private string _photoString;


        public ImageViewHolder(View itemView, Action<Post> click) : base(itemView)
        {
            _click = click;
            _photo = (ImageView)itemView;
            _photo.Clickable = true;
            _photo.Click += OnClick;
        }

        private void OnClick(object sender, EventArgs e)
        {
            _click.Invoke(_post);
        }

        public void UpdateData(Post post, Context context, int cellSize)
        {
            _post = post;
            _context = context;
            _photoString = post.Photos?.FirstOrDefault();
            if (_photoString != null)
            {
                Picasso.With(_context).Load(_photoString).Placeholder(Resource.Color.rgb244_244_246).NoFade().Resize(cellSize, cellSize).CenterCrop().Into(_photo, OnSuccess, OnError);
            }
        }

        public void OnBitmapFailed(Drawable p0)
        {
        }

        public void OnBitmapLoaded(Bitmap p0, Picasso.LoadedFrom p1)
        {
            _photo.SetImageBitmap(p0);
        }

        public void OnPrepareLoad(Drawable p0)
        {
        }

        private void OnSuccess()
        {
        }

        private void OnError()
        {
            Picasso.With(_context).Load(_photoString).Placeholder(Resource.Color.rgb244_244_246).NoFade().Into(this);
        }
    }
}