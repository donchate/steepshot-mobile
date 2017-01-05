﻿using System.Threading.Tasks;
using Steemix.Library.Models.Requests;

namespace Steemix.Android
{
    public class SignInViewModel : MvvmViewModelBase
    {
        public async Task<bool> SignIn(string login, string password)
        {
            var request = new LoginRequest(login, password);
            var response = await Manager.Login(request);
            if (response != null)
            {
                UserPrincipal.CreatePrincipal(response, login, password);
                return true;
            }
            else
                return false;
        }
    }
}
