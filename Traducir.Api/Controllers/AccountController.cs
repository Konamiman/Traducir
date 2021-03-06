using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Traducir.Api.ViewModels.Account;
using Traducir.Core.Helpers;
using Traducir.Core.Models;
using Traducir.Core.Models.Enums;
using Traducir.Core.Services;

namespace Traducir.Api.Controllers
{
    public class AccountController : Controller
    {
        private readonly ISEApiService _seApiService;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IAuthorizationService _authorizationService;

        public AccountController(
            IConfiguration configuration,
            ISEApiService seApiService,
            IUserService userService,
            IAuthorizationService authorizationService)
        {
            _seApiService = seApiService;
            _configuration = configuration;
            _userService = userService;
            _authorizationService = authorizationService;
        }

        [Route("app/login")]
        public IActionResult LogIn(string returnUrl)
        {
            return Redirect(_seApiService.GetInitialOauthUrl(GetOauthReturnUrl(), returnUrl));
        }

        [Route("app/logout")]
        public async Task<IActionResult> LogOut(string returnUrl = null)
        {
            await HttpContext.SignOutAsync();
            return Redirect(returnUrl ?? "/");
        }

        [Route("app/oauth-callback")]
        public async Task<IActionResult> OauthCallback(string code, string state = null)
        {
            var siteDomain = _configuration.GetValue<string>("STACKAPP_SITEDOMAIN");

            var accessToken = await _seApiService.GetAccessTokenFromCodeAsync(code, GetOauthReturnUrl());
            var currentUser = await _seApiService.GetMyUserAsync(siteDomain, accessToken);

            if (currentUser == null)
            {
                return Content("Could not retrieve a user account on " + siteDomain);
            }

            var minRep = _configuration.GetValue<int>("MIN_REP_TO_LOGIN");
            if (currentUser.Reputation < minRep)
            {
                return Content($"You need at least {minRep} to log in");
            }

            await _userService.UpsertUserAsync(new User
            {
                Id = currentUser.UserId,
                DisplayName = currentUser.DisplayName,
                IsModerator = currentUser.UserType == "moderator",
                CreationDate = DateTime.UtcNow,
                LastSeenDate = DateTime.UtcNow
            });

            var user = await _userService.GetUserAsync(currentUser.UserId);

            var claims = new List<Claim>
            {
                new Claim(ClaimType.Id, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimType.Name, user.DisplayName),
                new Claim(ClaimType.UserType, user.UserType.ToString())
            };
            if (user.UserType >= UserType.User)
            {
                claims.Add(new Claim(ClaimType.CanSuggest, "1"));
                if (user.UserType >= UserType.TrustedUser)
                {
                    claims.Add(new Claim(ClaimType.CanReview, "1"));
                }
            }

            if (user.IsModerator)
            {
                claims.Add(new Claim(ClaimType.IsModerator, "1"));
            }

            var identity = new ClaimsIdentity(claims, "login");

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Redirect(state ?? "/");
        }

        [Authorize]
        [Route("app/api/me")]
        public async Task<IActionResult> WhoAmI()
        {
            var canSuggest = (await _authorizationService.AuthorizeAsync(User, TraducirPolicy.CanSuggest)).Succeeded;
            var canReview = (await _authorizationService.AuthorizeAsync(User, TraducirPolicy.CanReview)).Succeeded;
            var canManageUsers = (await _authorizationService.AuthorizeAsync(User, TraducirPolicy.CanManageUsers)).Succeeded;

            return Json(new UserInfo
            {
                Name = User.GetClaim<string>(ClaimType.Name),
                UserType = User.GetClaim<UserType>(ClaimType.UserType),
                CanSuggest = canSuggest,
                CanReview = canReview,
                CanManageUsers = canManageUsers,
                Id = User.GetClaim<int>(ClaimType.Id)
            });
        }

        [HttpGet]
        [Authorize]
        [Route("app/api/me/notification-settings")]
        public async Task<IActionResult> NotificationSettings()
        {
            var userId = User.GetClaim<int>(ClaimType.Id);
            return Json(await _userService.GetNotificationSettings(userId));
        }

        [HttpPut]
        [Authorize]
        [Route("app/api/me/notification-settings")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsViewModel model)
        {
            var userId = User.GetClaim<int>(ClaimType.Id);
            if (!await _userService.UpdateNotificationSettings(userId, model.Notifications))
            {
                return BadRequest();
            }

            if (!await _userService.AddNotificationBrowser(userId, model.Subscription.ToWebPushSubscription()))
            {
                return BadRequest();
            }

            return NoContent();
        }

        [Authorize]
        [Route("app/api/users")]
        public async Task<IActionResult> GetUsers()
        {
            return Json((await _userService.GetUsersAsync())
                .OrderByDescending(u => u.UserType)
                .ThenByDescending(u => u.LastSeenDate));
        }

        [HttpPut]
        [Authorize(Policy = TraducirPolicy.CanManageUsers)]
        [Route("app/api/users/change-type")]
        public async Task<IActionResult> ChangeUserType([FromBody] ChangeUserTypeViewModel model)
        {
            // explicitly whitelist accepted types
            if (model.UserType != UserType.Banned && model.UserType != UserType.User && model.UserType != UserType.TrustedUser)
            {
                return BadRequest();
            }

            var success = await _userService.ChangeUserTypeAsync(model.UserId, model.UserType, User.GetClaim<int>(ClaimType.Id))
                ;
            if (!success)
            {
                return BadRequest();
            }

            return NoContent();
        }

        private string GetOauthReturnUrl()
        {
            return Url.Action("OauthCallback", null, null, _configuration.GetValue<bool>("USE_HTTPS") ? "https" : "http");
        }
    }
}