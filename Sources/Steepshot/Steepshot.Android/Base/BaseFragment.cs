﻿using System.Collections.Generic;
using Android.App;
using Android.Content;
using Steepshot.Core;

namespace Steepshot.Base
{
    public abstract class BaseFragment : Android.Support.V4.App.Fragment, IBaseView
    {
        protected bool IsInitialized;
        protected Android.Views.View V;

        public override void OnViewCreated(Android.Views.View view, Android.OS.Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            IsInitialized = true;
        }

        public Context GetContext()
        {
            return Context;
        }

        public virtual bool CustomUserVisibleHint
        {
            get;
            set;
        }

        protected virtual void ShowAlert(int messageid)
        {
            Show(GetString(messageid));
        }

        protected virtual void ShowAlert(string message)
        {
            Show(message);
        }

        protected virtual void ShowAlert(List<string> messages)
        {
            Show(messages[0]);
            //   Show(string.Join(System.Environment.NewLine, messages));
        }

        private void Show(string text)
        {
            var alert = new AlertDialog.Builder(Context);
            alert.SetMessage(text);
            alert.SetPositiveButton(Localization.Messages.Ok, (senderAlert, args) => { });
            Dialog dialog = alert.Create();
            dialog.Show();
        }
    }
}