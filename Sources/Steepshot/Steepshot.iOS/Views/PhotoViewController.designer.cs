// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace Steepshot.iOS.Views
{
	[Register ("PhotoViewController")]
	partial class PhotoViewController
	{
		[Outlet]
		UIKit.UIButton closeButton { get; set; }

		[Outlet]
		UIKit.UIButton enableCameraAccess { get; set; }

		[Outlet]
		UIKit.UIButton flashButton { get; set; }

		[Outlet]
		UIKit.UIView liveCameraStream { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (closeButton != null) {
				closeButton.Dispose ();
				closeButton = null;
			}

			if (flashButton != null) {
				flashButton.Dispose ();
				flashButton = null;
			}

			if (liveCameraStream != null) {
				liveCameraStream.Dispose ();
				liveCameraStream = null;
			}

			if (enableCameraAccess != null) {
				enableCameraAccess.Dispose ();
				enableCameraAccess = null;
			}
		}
	}
}
