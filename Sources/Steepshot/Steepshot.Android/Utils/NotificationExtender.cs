﻿using Android.App;
using Android.Content;
using Android.Support.V4.App;
using Com.OneSignal.Android;
using Java.Lang;

namespace Steepshot.Utils
{
    [Service(Name = "com.droid.steepshot.NotificationExtender", Exported = false, Permission = "android.permission.BIND_JOB_SERVICE")]
    [IntentFilter(new[] { "com.onesignal.NotificationExtender" })]
    public class NotificationExtender : NotificationExtenderService, NotificationCompat.IExtender
    {
        private OSNotificationReceivedResult _result;
        protected override void OnHandleIntent(Intent intent)
        {
        }

        protected override bool OnNotificationProcessing(OSNotificationReceivedResult p0)
        {
            _result = p0;
            var overrideSettings = new OverrideSettings { Extender = this };
            DisplayNotification(overrideSettings);
            return true;
        }

        public NotificationCompat.Builder Extend(NotificationCompat.Builder builder)
        {
            builder.SetSmallIcon(Resource.Drawable.ic_notification)
                .SetContentTitle(_result.Payload.Title)
                .SetContentText(_result.Payload.Body)
                .SetStyle(new NotificationCompat.InboxStyle()
                .AddLine("")
                .AddLine("")
                .SetBigContentTitle("big titel")
                .SetSummaryText("sum text"))
                .SetGroup(_result.Payload.GroupKey)
                .SetGroupSummary(true);
            return builder;
        }
    }
}