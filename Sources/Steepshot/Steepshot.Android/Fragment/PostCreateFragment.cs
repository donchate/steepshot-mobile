﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Java.IO;
using Steepshot.Core;
using Steepshot.Core.Exceptions;
using Steepshot.Core.Localization;
using Steepshot.Core.Models.Common;
using Steepshot.Core.Models.Requests;
using Steepshot.Core.Utils;
using Steepshot.Utils;

namespace Steepshot.Fragment
{
    public class PostCreateFragment : PostPrepareBaseFragment
    {
        public static string PostCreateGalleryTemp = "PostCreateGalleryTemp" + AppSettings.User.Login;
        public static string PreparePostTemp = "PreparePostTemp" + AppSettings.User.Login;
        private readonly PreparePostModel _tepmPost;

        public PostCreateFragment(List<GalleryMediaModel> media, PreparePostModel model) : base(media)
        {
            _tepmPost = model;
        }

        public PostCreateFragment(List<GalleryMediaModel> media) : base(media)
        {
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            if (IsInitialized)
                return;

            base.OnViewCreated(view, savedInstanceState);

            if (_tepmPost != null)
            {
                _model.Media = _tepmPost.Media;
                _title.Text = _model.Title;
                _description.Text = _model.Description;
                for (var i = 0; i < _model.Tags.Length; i++)
                    _localTagsAdapter.LocalTags.Add(_model.Tags[i]);
            }

            InitData();
            SearchTextChanged();
        }

        protected virtual async void InitData()
        {
            if (_media.Count > 1)
            {
                _photos.Visibility = ViewStates.Visible;
                _previewContainer.Visibility = ViewStates.Gone;
                _photos.SetLayoutManager(new LinearLayoutManager(Activity, LinearLayoutManager.Horizontal, false));
                _photos.AddItemDecoration(new ListItemDecoration(Style.Margin10));
                _photos.LayoutParameters.Height = Style.GalleryHorizontalHeight;

                _photos.SetAdapter(GalleryAdapter);

                await ConvertAndSave();
                if (!IsInitialized)
                    return;
            }
            else
            {
                _photos.Visibility = ViewStates.Gone;
                _previewContainer.Visibility = ViewStates.Visible;
                _preview.CornerRadius = Style.CornerRadius5;
                _ratioBtn.Visibility = ViewStates.Gone;
                _rotateBtn.Visibility = ViewStates.Gone;

                await ConvertAndSave();
                if (!IsInitialized)
                    return;

                var previewSize = BitmapUtils.CalculateImagePreviewSize(_media[0].Parameters, Style.ScreenWidth - Style.Margin15 * 2);
                var layoutParams = new RelativeLayout.LayoutParams(previewSize.Width, previewSize.Height);
                layoutParams.SetMargins(Style.Margin15, 0, Style.Margin15, Style.Margin15);
                _previewContainer.LayoutParameters = layoutParams;
                _preview.SetImageBitmap(_media[0].PreparedBitmap);

                _preview.Touch += PreviewOnTouch;
            }

            await CheckOnSpam(false);
            if (!IsInitialized)
                return;

            if (isSpammer)
                return;

            StartUploadMedia();
        }

        private void PreviewOnTouch(object sender, View.TouchEventArgs touchEventArgs)
        {
            //event interception
            touchEventArgs.Handled = true;
        }

        protected async Task ConvertAndSave()
        {
            if (_media.All(m => m.UploadState != UploadState.ReadyToSave))
                return;

            await Task.Run(() =>
            {
                foreach (var model in _media)
                {
                    if (model.UploadState == UploadState.ReadyToSave)
                    {
                        var croppedBitmap = BitmapUtils.Crop(Context, model.Path, model.Parameters);
                        model.PreparedBitmap = croppedBitmap;
                        model.TempPath = SaveFileTemp(model.PreparedBitmap, model.Path);
                        model.UploadState = UploadState.Saved;

                        if (_media.Count > 1)
                            Activity.RunOnUiThread(() => GalleryAdapter.NotifyDataSetChanged());
                    }
                }
                SaveGalleryTemp();
            });
        }

        private bool _isUploading = false;
        private async Task StartUploadMedia()
        {
            _isUploading = true;
            await RepeatUpload();
            if (!IsInitialized)
                return;

            await RepeatVerifyUpload();
            if (!IsInitialized)
                return;

            await GetMediaModel();
            if (!IsInitialized)
                return;

            _isUploading = false;
        }

        private async Task RepeatUpload()
        {
            var maxRepeat = 3;
            var repeatCount = 0;

            do
            {
                for (var i = 0; i < _media.Count; i++)
                {
                    var media = _media[i];
                    if (!(media.UploadState == UploadState.Saved || media.UploadState == UploadState.UploadError))
                        continue;

                    var operationResult = await UploadMedia(media);

                    if (!IsInitialized)
                        return;

                    if (!operationResult.IsSuccess)
                        Activity.ShowAlert(operationResult.Exception, ToastLength.Short);
                    else
                    {
                        media.UploadMediaUuid = operationResult.Result;
                        SaveGalleryTemp();
                    }
                }


                if (_media.All(m => m.UploadState == UploadState.UploadEnd))
                    break;

                repeatCount++;

            } while (repeatCount < maxRepeat);
        }

        private async Task<OperationResult<UUIDModel>> UploadMedia(GalleryMediaModel model)
        {
            model.UploadState = UploadState.UploadStart;
            System.IO.Stream stream = null;
            FileInputStream fileInputStream = null;

            try
            {
                var photo = new Java.IO.File(model.TempPath);
                fileInputStream = new FileInputStream(photo);
                stream = new StreamConverter(fileInputStream, null);

                var request = new UploadMediaModel(AppSettings.User.UserInfo, stream, System.IO.Path.GetExtension(model.TempPath));
                var serverResult = await Presenter.TryUploadMedia(request);
                model.UploadState = UploadState.UploadEnd;
                return serverResult;
            }
            catch (Exception ex)
            {
                model.UploadState = UploadState.UploadError;
                await AppSettings.Logger.Error(ex);
                return new OperationResult<UUIDModel>(new InternalException(LocalizationKeys.PhotoUploadError, ex));
            }
            finally
            {
                fileInputStream?.Close(); // ??? change order?
                stream?.Flush();
                fileInputStream?.Dispose();
                stream?.Dispose();
            }
        }

        private async Task RepeatVerifyUpload()
        {
            do
            {
                for (var i = 0; i < _media.Count; i++)
                {
                    var media = _media[i];
                    if (media.UploadState != UploadState.UploadEnd)
                        continue;

                    var operationResult = await Presenter.TryGetMediaStatus(media.UploadMediaUuid);
                    if (!IsInitialized)
                        return;

                    if (operationResult.IsSuccess)
                    {
                        switch (operationResult.Result.Code)
                        {
                            case UploadMediaCode.Done:
                                {
                                    media.UploadState = UploadState.UploadVerified;
                                    SaveGalleryTemp();
                                }
                                break;
                            case UploadMediaCode.FailedToProcess:
                            case UploadMediaCode.FailedToUpload:
                            case UploadMediaCode.FailedToSave:
                                {
                                    media.UploadState = UploadState.UploadError;
                                    SaveGalleryTemp();
                                }
                                break;
                        }
                    }
                }

                if (_media.All(m => m.UploadState != UploadState.UploadEnd))
                    break;

                await Task.Delay(3000);
                if (!IsInitialized)
                    return;

            } while (true);
        }

        private async Task GetMediaModel()
        {
            if (_model.Media == null)
                _model.Media = new MediaModel[_media.Count];

            for (var i = 0; i < _media.Count; i++)
            {
                var media = _media[i];
                if (media.UploadState != UploadState.UploadVerified)
                    continue;

                var mediaResult = await Presenter.TryGetMediaResult(media.UploadMediaUuid);
                if (!IsInitialized)
                    return;

                if (mediaResult.IsSuccess)
                {
                    _model.Media[i] = mediaResult.Result;
                    media.UploadState = UploadState.Ready;
                    SaveGalleryTemp();
                }

                if (!IsInitialized)
                    return;
            }
        }

        protected override async Task OnPostAsync()
        {
            _model.Title = _title.Text;
            _model.Description = _description.Text;
            _model.Tags = _localTagsAdapter.LocalTags.ToArray();

            SavePreparePostTemp();

            EnablePostAndEdit(false, true);

            while (_isUploading)
            {
                await Task.Delay(300);
                if (!IsInitialized)
                    return;
            }

            if (_media.Any(m => m.UploadState != UploadState.Ready))
            {
                await CheckOnSpam(true);
                if (isSpammer || !IsInitialized)
                    return;

                await StartUploadMedia();
                if (!IsInitialized)
                    return;
            }

            if (_media.Any(m => m.UploadState != UploadState.Ready))
            {
                Activity.ShowAlert(LocalizationKeys.PhotoUploadError, ToastLength.Long);
            }
            else
            {
                var isCreated = await TryCreateOrEditPost();
                if (isCreated)
                    Activity.ShowAlert(LocalizationKeys.PostDelay, ToastLength.Long);
            }

            EnablePostAndEdit(true);
        }

        protected async Task CheckOnSpam(bool disableEditing)
        {
            EnablePostAndEdit(false, disableEditing);
            isSpammer = false;

            var spamCheck = await Presenter.TryCheckForSpam(AppSettings.User.Login);
            if (!IsInitialized)
                return;

            if (spamCheck.IsSuccess)
            {
                if (!spamCheck.Result.IsSpam)
                {
                    if (spamCheck.Result.WaitingTime > 0)
                    {
                        isSpammer = true;
                        PostingLimit = TimeSpan.FromMinutes(5);
                        StartPostTimer((int)spamCheck.Result.WaitingTime);
                        Activity.ShowAlert(LocalizationKeys.Posts5minLimit, ToastLength.Long);
                    }
                    else
                    {
                        EnabledPost();
                    }
                }
                else
                {
                    // more than 15 posts
                    isSpammer = true;
                    PostingLimit = TimeSpan.FromHours(24);
                    StartPostTimer((int)spamCheck.Result.WaitingTime);
                    Activity.ShowAlert(LocalizationKeys.PostsDayLimit, ToastLength.Long);
                }
            }

            EnablePostAndEdit(true);
        }

        private async void StartPostTimer(int startSeconds)
        {
            var timepassed = PostingLimit - TimeSpan.FromSeconds(startSeconds);

            while (timepassed < PostingLimit)
            {
                var delay = PostingLimit - timepassed;
                var timeFormat = delay.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss";
                _postButton.Text = delay.ToString(timeFormat);
                _postButton.Enabled = false;

                await Task.Delay(1000);
                if (!IsInitialized)
                    return;

                timepassed = timepassed.Add(TimeSpan.FromSeconds(1));
            }

            isSpammer = false;
            EnabledPost();
        }

        private string SaveFileTemp(Bitmap btmp, string pathToExif)
        {
            FileStream stream = null;
            try
            {
                var directory = new Java.IO.File(Context.CacheDir, Constants.Steepshot);
                if (!directory.Exists())
                    directory.Mkdirs();

                var path = $"{directory}/{Guid.NewGuid()}.jpeg";
                stream = new FileStream(path, FileMode.Create);
                btmp.Compress(Bitmap.CompressFormat.Jpeg, 99, stream);

                var options = new Dictionary<string, string>
                {
                    {ExifInterface.TagImageLength, btmp.Height.ToString()},
                    {ExifInterface.TagImageWidth, btmp.Width.ToString()},
                    {ExifInterface.TagOrientation, "1"},
                };

                BitmapUtils.CopyExif(pathToExif, path, options);

                return path;
            }
            catch (Exception ex)
            {
                _postButton.Enabled = false;
                AppSettings.Logger.Error(ex);
                Context.ShowAlert(ex);
            }
            finally
            {
                stream?.Dispose();
            }
            return string.Empty;
        }

        public override void OnDetach()
        {
            CleanCash();
            base.OnDetach();
        }

        private void CleanCash()
        {
            var files = Context.CacheDir.ListFiles();
            foreach (var file in files)
                if (file.Path.EndsWith(Constants.Steepshot))
                    file.Delete();
        }

        protected override void OnPostSuccess()
        {
            var isChanged = false;
            if (AppSettings.Temp.ContainsKey(PostCreateGalleryTemp))
            {
                AppSettings.Temp.Remove(PostCreateGalleryTemp);
                isChanged = true;
            }

            if (AppSettings.Temp.ContainsKey(PreparePostTemp))
            {
                AppSettings.Temp.Remove(PreparePostTemp);
                isChanged = true;
            }

            if (isChanged)
                AppSettings.SaveTemp();
        }

        private void SaveGalleryTemp()
        {
            //TODO: KOA UI not support Respo

            //var json = JsonConvert.SerializeObject(_media);
            //if (AppSettings.Temp.ContainsKey(PostCreateGalleryTemp))
            //    AppSettings.Temp[PostCreateGalleryTemp] = json;
            //else
            //    AppSettings.Temp.Add(PostCreateGalleryTemp, json);
            //AppSettings.SaveTemp();
        }

        private void SavePreparePostTemp()
        {
            //var json = JsonConvert.SerializeObject(_model);
            //if (AppSettings.Temp.ContainsKey(PreparePostTemp))
            //    AppSettings.Temp[PreparePostTemp] = json;
            //else
            //    AppSettings.Temp.Add(PreparePostTemp, json);
            //AppSettings.SaveTemp();
        }
    }
}
