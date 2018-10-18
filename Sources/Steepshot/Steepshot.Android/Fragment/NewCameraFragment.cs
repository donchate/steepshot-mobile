﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using CheeseBind;
using Java.IO;
using Refractored.Controls;
using Steepshot.Base;
using Steepshot.CameraGL;
using Steepshot.CameraGL.Encoder;
using Steepshot.CustomViews;
using Steepshot.Utils;
using Steepshot.Core;
#pragma warning disable 618
using Camera = Android.Hardware.Camera;
#pragma warning restore 618

namespace Steepshot.Fragment
{
#pragma warning disable 0649, 4014, 0618
    public class NewCameraFragment : BaseFragment, Camera.IShutterCallback, Camera.IPictureCallback, ILocationListener
    {
        private CameraConfig _cameraConfig;
        private CameraManager _cameraManager;
        private readonly File _directory;
        private readonly MuxerWrapper _muxerWrapper;
        private readonly VideoEncoderConfig _videoEncoderConfig;
        private readonly AudioEncoderConfig _audioEncoderConfig;
        private GradientDrawable _btnsBackground;
        private float _fingerDist;
        private readonly int _maxVideoDurationMs = Constants.VideoMaxDuration * 1000 / 2;
        private DateTime _recordingStart;

        private Location _currentLocation;
        private LocationManager _locationManager;
        private bool _isGpsEnable;

        [BindView(Resource.Id.top)] private RelativeLayout _topPanel;
        [BindView(Resource.Id.camera_preview_afl)] private AspectFrameLayout _aspectFrameLayout;
        [BindView(Resource.Id.camera_preview_surface)] private SurfaceView _surface;
        [BindView(Resource.Id.tabs)] private TabLayout _tabs;
        [BindView(Resource.Id.gallery_icon)] private CircleImageView _galleryBtn;
        [BindView(Resource.Id.shot_btn)] private CameraShotButton _shotBtn;
        [BindView(Resource.Id.shot_btn_loading)] private ProgressBar _shotBtnLoading;
        [BindView(Resource.Id.close)] private ImageButton _closeBtn;
        [BindView(Resource.Id.flash)] private ImageButton _flashBtn;
        [BindView(Resource.Id.gps_button)] private ImageButton _gpsButton;
        [BindView(Resource.Id.revert)] private ImageButton _revertBtn;

#pragma warning restore 0649

        public NewCameraFragment()
        {
            _directory = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDcim);
            _muxerWrapper = new MuxerWrapper();
            _videoEncoderConfig = new VideoEncoderConfig(_muxerWrapper, 720, 720, "video/avc", 30, 2, 3000000, Constants.VideoMaxDuration);
            _audioEncoderConfig = new AudioEncoderConfig(_muxerWrapper, "audio/mp4a-latm", 44100, 2048, 128000, ChannelIn.Mono, Constants.VideoMaxDuration);
            _muxerWrapper.MuxingFinished += VideoRecorded;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var v = inflater.Inflate(Resource.Layout.lyt_new_camera, null);
            Cheeseknife.Bind(this, v);
            return v;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            _btnsBackground = new GradientDrawable();
            _btnsBackground.SetCornerRadius(TypedValue.ApplyDimension(ComplexUnitType.Dip, 100, Activity.Resources.DisplayMetrics));
            _btnsBackground.SetColors(new int[] { Color.Black, Color.Black });

            _cameraManager = new CameraManager(Activity, _surface)
            {
                SupportFlashModes = new[]
                    {Camera.Parameters.FlashModeAuto, Camera.Parameters.FlashModeOn, Camera.Parameters.FlashModeOff}
            };
            _cameraConfig = CameraConfig.Photo;

            _tabs.AddTab(_tabs.NewTab().SetText("Photo"), true);
            _tabs.AddTab(_tabs.NewTab().SetText("Video"));
            _tabs.TabSelected += CameraModeChanged;

            _gpsButton.SetImageResource(Resource.Drawable.ic_gps_n);
            _closeBtn.Background = _gpsButton.Background = _flashBtn.Background = _revertBtn.Background = _btnsBackground;

            SetGalleryIcon();
            UpdateUi();

            _shotBtn.Touch += ShotBtnOnTouch;
            _closeBtn.Click += CloseBtnOnClick;
            _flashBtn.Click += SetFlashButton;
            _revertBtn.Click += RevertBtnOnClick;
            _galleryBtn.Click += GalleryBtnOnClick;
            _surface.Touch += SurfaceOnTouch;
            _gpsButton.Click += GpsButtonOnClick;
        }

        public override void OnDetach()
        {
            base.OnDetach();
            Cheeseknife.Reset(this);
        }

        public override void OnPause()
        {
            base.OnPause();
            _cameraManager.OnPause();
            _muxerWrapper.Interrupt();
        }

        public override void OnResume()
        {
            base.OnResume();
            _cameraManager.OnResume();
            _cameraManager.ReConfigure(_cameraManager.CurrentCamera, _videoEncoderConfig,
                _cameraConfig == CameraConfig.Photo ? null : _audioEncoderConfig);
            SetFlashButton(null, null);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            if (requestCode == BaseActivity.CommonPermissionsRequestCode && !grantResults.Any(x => x != Permission.Granted))
            {
                _locationManager = (LocationManager)Context.GetSystemService(Android.Content.Context.LocationService);
                var criteriaForLocationService = new Criteria { Accuracy = Accuracy.NoRequirement };
                _locationManager.RequestLocationUpdates(0, 0, criteriaForLocationService, this, null);
                GpsButtonOnClick(null, null);
            }
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private void GpsButtonOnClick(object sender, EventArgs eventArgs)
        {
            SetUiEnable(false);

            if (!((BaseActivity)Activity).RequestPermissions(BaseActivity.CommonPermissionsRequestCode, Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation))
            {
                _isGpsEnable = !_isGpsEnable;
            }

            _gpsButton.SetImageResource(_isGpsEnable ? Resource.Drawable.ic_gps : Resource.Drawable.ic_gps_n);
            SetUiEnable(true);
        }

        private void UpdateUi()
        {
            var lytParams = (RelativeLayout.LayoutParams)_aspectFrameLayout.LayoutParameters;
            var shotBtnLytParams = (ViewGroup.MarginLayoutParams)_shotBtn.LayoutParameters;
            var margin = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 10, Activity.Resources.DisplayMetrics);
            var availableSpace = Style.ScreenHeight - Style.ScreenWidth - _topPanel.LayoutParameters.Height - _tabs.LayoutParameters.Height;
            shotBtnLytParams.Width = Math.Min(shotBtnLytParams.Width, availableSpace - margin * 2);
            shotBtnLytParams.BottomMargin = availableSpace - shotBtnLytParams.Width - margin;

            var shotBtnLoadingLytParams = (ViewGroup.MarginLayoutParams)_shotBtnLoading.LayoutParameters;
            shotBtnLoadingLytParams.Width = shotBtnLoadingLytParams.Height = (int)(shotBtnLytParams.Width / 1.5f);
            shotBtnLoadingLytParams.BottomMargin = shotBtnLytParams.BottomMargin;

            _shotBtnLoading.LayoutParameters = shotBtnLoadingLytParams;

            switch (_cameraConfig)
            {
                case CameraConfig.Photo:
                    lytParams.RemoveRule(LayoutRules.Below);
                    _aspectFrameLayout.SetAspectRatio(Style.ScreenWidth / (float)Style.ScreenHeight);
                    ((RelativeLayout)_aspectFrameLayout.Parent).UpdateViewLayout(_aspectFrameLayout, lytParams);
                    _shotBtn.Background = new ColorDrawable(Style.R217G217B217);
                    _shotBtn.Color = Color.White;
                    _btnsBackground.Alpha = 205;
                    _gpsButton.Visibility =
                    _flashBtn.Visibility = ViewStates.Visible;
                    _closeBtn.SetImageResource(Resource.Drawable.ic_close);
                    _flashBtn.SetImageResource(Resource.Drawable.ic_flash);
                    _revertBtn.SetImageResource(Resource.Drawable.ic_revert_white);
                    _tabs.SetTabTextColors(Color.White, Color.White);
                    break;
                case CameraConfig.Video:
                    lytParams.AddRule(LayoutRules.Below, Resource.Id.top);
                    _aspectFrameLayout.SetAspectRatio(1f);
                    ((RelativeLayout)_aspectFrameLayout.Parent).UpdateViewLayout(_aspectFrameLayout, lytParams);
                    _shotBtn.Background = new ColorDrawable(Color.White);
                    _shotBtn.Color = Style.R255G34B5;
                    _btnsBackground.Alpha = 12;
                    _gpsButton.Visibility =
                    _flashBtn.Visibility = ViewStates.Gone;
                    _closeBtn.SetImageResource(Resource.Drawable.ic_close_black);
                    _revertBtn.SetImageResource(Resource.Drawable.ic_revert_black);
                    _tabs.SetTabTextColors(Color.Black, Color.Black);
                    break;
            }

            _revertBtn.Visibility = _cameraManager.SupportedCameras.Count == 2 ? ViewStates.Visible : ViewStates.Gone;

            _closeBtn.Invalidate();
            _flashBtn.Invalidate();
            _revertBtn.Invalidate();
        }

        private void SetUiEnable(bool enable)
        {
            _tabs.Enabled = _gpsButton.Enabled = _flashBtn.Enabled = _revertBtn.Enabled = _shotBtn.Enabled = enable;
        }

        private async void CameraModeChanged(object sender, TabLayout.TabSelectedEventArgs e)
        {
            SetUiEnable(false);
            _cameraConfig = e.Tab.Position == 0 ? CameraConfig.Photo : CameraConfig.Video;
            UpdateUi();
            await Task.Run(() => _cameraManager.ReConfigure(_cameraManager.CurrentCamera, _videoEncoderConfig,
                _cameraConfig == CameraConfig.Photo ? null : _audioEncoderConfig));
            SetFlashButton(null, null);
            SetUiEnable(true);
        }

        private void SetGalleryIcon()
        {
            string[] projection = {
                MediaStore.Images.ImageColumns.Id,
                MediaStore.Images.ImageColumns.Data
            };
            var cursor = Context.ContentResolver
                .Query(MediaStore.Images.Media.ExternalContentUri, projection, null, null, MediaStore.Images.ImageColumns.DateTaken + " DESC");

            if (cursor != null && cursor.Count > 0)
            {
                var idInd = cursor.GetColumnIndex(MediaStore.Images.ImageColumns.Id);
                var pathInd = cursor.GetColumnIndex(MediaStore.Images.ImageColumns.Data);

                while (cursor.MoveToNext())
                {
                    var imageId = cursor.GetLong(idInd);
                    var imageLocation = cursor.GetString(pathInd);
                    var imageFile = new File(imageLocation);
                    if (imageFile.Exists())
                    {
                        using (var bitmap = MediaStore.Images.Thumbnails.GetThumbnail(Context.ContentResolver, imageId,
                            ThumbnailKind.MicroKind, null))
                        {
                            _galleryBtn.SetImageBitmap(bitmap);
                        }
                        break;
                    }
                }
            }
            else
            {
                _galleryBtn.SetImageDrawable(new ColorDrawable(Style.R245G245B245));
            }
        }


        private void CloseBtnOnClick(object sender, EventArgs e)
        {
            _closeBtn.Enabled = false;
            Activity.OnBackPressed();
            _closeBtn.Enabled = true;
        }

        private void SetFlashButton(object sender, EventArgs e)
        {
            SetUiEnable(false);
            if (sender != null)
                _cameraManager.ToggleFlashMode();

            switch (_cameraManager.CurrentCamera.FlashMode)
            {
                case Camera.Parameters.FlashModeAuto:
                    _flashBtn.SetImageResource(Resource.Drawable.ic_flash_auto);
                    break;
                case Camera.Parameters.FlashModeOff:
                    _flashBtn.SetImageResource(Resource.Drawable.ic_flash_off);
                    break;
                default:
                    _flashBtn.SetImageResource(Resource.Drawable.ic_flash);
                    break;
            }

            SetUiEnable(true);
        }

        private void RevertBtnOnClick(object sender, EventArgs e)
        {
            SetUiEnable(false);
            var cameraInfo = _cameraManager.SupportedCameras.Find(x => x.Info.Facing != _cameraManager.CurrentCamera.Info.Facing);
            _cameraManager.ReConfigure(cameraInfo, _videoEncoderConfig,
                _cameraConfig == CameraConfig.Photo ? null : _audioEncoderConfig);
            SetFlashButton(null, null);
            SetUiEnable(true);
        }

        private void GalleryBtnOnClick(object sender, EventArgs e)
        {
            SetUiEnable(false);
            ((BaseActivity)Activity).OpenNewContentFragment(new GalleryFragment());
        }

        private async void ShotBtnOnTouch(object sender, View.TouchEventArgs e)
        {
            switch (e.Event.Action)
            {
                case MotionEventActions.Move:
                    _shotBtn.Pressed = true;
                    if (_cameraConfig == CameraConfig.Video && !_cameraManager.RecordingEnabled)
                    {
                        _muxerWrapper.Reset($"{_directory}/{Guid.NewGuid()}.mp4", MuxerOutputType.Mpeg4);
                        _cameraManager.ToggleRecording();
                        _recordingStart = DateTime.Now;
                        while (_cameraManager.RecordingEnabled)
                        {
                            var elapsedTime = DateTime.Now - _recordingStart;
                            _shotBtn.Progress = (float)(100 * elapsedTime.TotalMilliseconds / _maxVideoDurationMs);
                            if (elapsedTime.TotalMilliseconds >= _maxVideoDurationMs)
                            {
                                _shotBtn.Touch -= ShotBtnOnTouch;
                                FinishRecording();
                                return;
                            }
                            await Task.Delay(25);
                        }
                    }
                    break;
                case MotionEventActions.Up:
                    _shotBtn.Pressed = false;
                    if (_cameraConfig == CameraConfig.Photo)
                        TakePhoto();
                    else
                        FinishRecording();
                    break;
            }
        }

        private void TakePhoto()
        {
            SetUiEnable(false);
            _shotBtn.Visibility = ViewStates.Gone;
            _shotBtnLoading.Visibility = ViewStates.Visible;
            try
            {
                var camParams = _cameraManager?.Parameters;
                AddGps(camParams);
                camParams?.SetRotation(_cameraManager.CurrentCamera.Rotation);
                _cameraManager?.SetParameters(camParams);
                _cameraManager?.TakePicture(this, null, this);
            }
            catch (Exception ex)
            {
                SetUiEnable(true);
                _shotBtn.Visibility = ViewStates.Visible;
                _shotBtnLoading.Visibility = ViewStates.Gone;
                App.Logger.WarningAsync(ex);
            }
        }

        private void AddGps(Camera.Parameters parameters)
        {
            if (_isGpsEnable && _currentLocation != null)
            {
                parameters.RemoveGpsData();
                parameters.SetGpsLatitude(_currentLocation.Latitude);
                parameters.SetGpsLongitude(_currentLocation.Longitude);
                parameters.SetGpsAltitude(_currentLocation.Altitude);
                parameters.SetGpsTimestamp(_currentLocation.Time);
                parameters.SetGpsProcessingMethod(_currentLocation.Provider);
            }
        }

        private void FinishRecording()
        {
            if ((DateTime.Now - _recordingStart).TotalSeconds < Constants.VideoMinDuration)
            {
                if (_cameraManager.RecordingEnabled)
                    _cameraManager.ToggleRecording();
                _shotBtn.Progress = 0;
            }
            else
            {
                _shotBtn.Visibility = ViewStates.Gone;
                _shotBtnLoading.Visibility = ViewStates.Visible;
                if (_cameraManager.RecordingEnabled)
                    _cameraManager.ToggleRecording(true);
            }
        }

        private void VideoRecorded(string path)
        {
            ((BaseActivity)Activity).OpenNewContentFragment(new VideoPostCreateFragment(path));
        }

        public void OnShutter()
        {
            SetUiEnable(false);
        }

        public void OnPictureTaken(byte[] data, Camera camera)
        {
            Task.Run(() =>
            {
                var photoUri = $"{_directory}/{Guid.NewGuid()}.jpeg";

                var stream = new FileOutputStream(photoUri);
                stream.Write(data);
                stream.Close();

                var exifInterface = new ExifInterface(photoUri);
                var orientation = exifInterface.GetAttributeInt(ExifInterface.TagOrientation, 0);

                if (orientation != 1 && orientation != 0)
                {
                    var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                    bitmap = BitmapUtils.RotateImage(bitmap, _cameraManager.CurrentCamera.Rotation);
                    var rotationStream = new System.IO.FileStream(photoUri, System.IO.FileMode.Create);
                    bitmap.Compress(Bitmap.CompressFormat.Jpeg, 100, rotationStream);
                }

                var model = new GalleryMediaModel
                {
                    Path = photoUri
                };

                Activity.RunOnUiThread(() =>
                {
                    ((BaseActivity)Activity).OpenNewContentFragment(new PreviewPostCreateFragment(model));
                });
            });
        }

        private void SurfaceOnTouch(object sender, View.TouchEventArgs e)
        {
            SetUiEnable(false);
            var camParams = _cameraManager?.Parameters;
            if (camParams == null)
                return;

            var action = e.Event.Action;

            if (e.Event.PointerCount > 1)
            {
                if (action == MotionEventActions.PointerDown)
                {
                    _fingerDist = GetFingerSpacing(e.Event);
                }
                else if (action == MotionEventActions.Move && camParams.IsZoomSupported)
                {
                    _cameraManager?.CancelAutoFocus();
                    HandleZoom(e.Event, camParams);
                }
            }
            SetUiEnable(true);

            e.Handled = true;
        }

        private void HandleZoom(MotionEvent e, Camera.Parameters p)
        {
            var maxZoom = p.MaxZoom;
            var zoom = p.Zoom;
            var newDist = GetFingerSpacing(e);
            if (newDist > _fingerDist)
            {
                if (zoom < maxZoom)
                    zoom++;
            }
            else if (newDist < _fingerDist)
            {
                if (zoom > 0)
                    zoom--;
            }
            _fingerDist = newDist;
            p.Zoom = zoom;
            _cameraManager?.SetParameters(p);
        }

        private float GetFingerSpacing(MotionEvent e)
        {
            var x = e.GetX(0) - e.GetX(1);
            var y = e.GetY(0) - e.GetY(1);
            return (float)Math.Sqrt(x * x + y * y);
        }

        public void OnLocationChanged(Location location)
        {
            if (_currentLocation == null)
            {
                _gpsButton.Visibility = ViewStates.Visible;
                _gpsButton.SetImageResource(Resource.Drawable.ic_gps);
            }
            if (_currentLocation == null || _currentLocation.Accuracy > location.Accuracy)
                _currentLocation = location;
        }

        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
        }
    }
}