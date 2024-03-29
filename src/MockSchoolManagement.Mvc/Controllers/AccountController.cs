﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MockSchoolManagement.Models;
using MockSchoolManagement.ViewModels;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MockSchoolManagement.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private UserManager<ApplicationUser> _userManager;
        private SignInManager<ApplicationUser> _signInManager;
        private ILogger<AccountController> _logger;

        public AccountController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        #region 登录
        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            LoginViewModel model = new LoginViewModel
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user != null && !user.EmailConfirmed &&
                    (await _userManager.CheckPasswordAsync(user, model.Password)))
                {
                    ModelState.AddModelError(string.Empty, "Email not confirmed yet");
                    return View(model);
                }

                var result =
                    await _signInManager.PasswordSignInAsync(
                        model.Email, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        if (Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                    }
                    else
                    {
                        return RedirectToAction("index", "home");
                    }
                }

                ModelState.AddModelError(string.Empty, "登录失败，请重试");
            }
            return View(model);
        }

        /// <summary>
        /// 扩展登录
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            LoginViewModel loginViewModel = new LoginViewModel
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            };

            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"第三方登录提供程序错误:{remoteError}");
                return View("Login", loginViewModel);
            }

            // 从第三方登录提供商，即微软账户体系中，获取关于用户的登录信息
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ModelState.AddModelError(string.Empty, "加载第三方登录信息出错。");
                return View("Login", loginViewModel);
            }

            // 获取邮箱地址
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            ApplicationUser user = null;
            if (email != null)
            {
                //通过邮箱地址去查询用户是否存在
                user = await _userManager.FindByEmailAsync(email);
                //如果电子邮箱没被确认，返回登录试图与验证错误
                if (user != null && !user.EmailConfirmed)
                {
                    ModelState.AddModelError(string.Empty, "您的电子邮箱还未进行验证。");
                    return View("Login", loginViewModel);
                }
            }

            //如果用户之前已经登录过了，则会在AspNetUserLogins表有对应的记录，这个
            //时候无须创建新的记录，直接使用当前记录登录系统即可
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider,
                info.ProviderKey, isPersistent:false, bypassTwoFactor:true);

            if (signInResult.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }
            //如果AspNetUserLogins表中没有记录，则代表用户没有一个本地账户，这个时候我们就需要
            //创建一个记录了
            else
            {
                if (email != null)
                {
                    if (user == null)
                    {
                        user = new ApplicationUser
                        { 
                            UserName = info.Principal.FindFirstValue(ClaimTypes.Email),
                            Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                        };
                        //如果不存在，则创建一个用户，但是这个用户没有密码
                        await _userManager.CreateAsync(user);
                    }

                    // 在AspNetUserLogins表中添加一行用户数据，然后将当前用户登录
                    // 到系统中
                    await _userManager.AddLoginAsync(user, info);
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return LocalRedirect(returnUrl);
                }


                //如果我们获取不到邮箱地址，则需要将请求重定向到错误视图中
                ViewBag.ErrorTitle = $"我们无法从提供商:{info.LoginProvider}中解析到读者的邮件地址 ";
                ViewBag.ErrorMessage = "请通过联系ltm@ddxc.org寻求技术支持。";
                return View("Error");
            }
        }
        #endregion

        #region 注册
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                //将数据从RegisterViewModel赋值到IdentityUser
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    City = model.City
                };

                //将用户数据存储在AspNetUsers数据库表中
                var result = await _userManager.CreateAsync(user, model.Password);

                //如果成功创建用户，则使用登录服务登录用户信息
                //并重定向到HomeController的索引操作
                if (result.Succeeded)
                {
                    //生成电子邮箱确认令牌
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    //生成电子邮箱的确认链接
                    var confirmationLink = Url.Action("ConfirmEmail", "Account",
                        new { userId = user.Id, token = token }, Request.Scheme);
                    //记录生成的URL链接
                    _logger.Log(LogLevel.Warning, confirmationLink);

                    //如果用户已登录且为Admin角色
                    //那么就是Admin正在创建新用户
                    //所以重定向Admin用户到ListUsers的视图列表
                    if (_signInManager.IsSignedIn(User)  &&  User.IsInRole("Admin"))
                    {
                        return RedirectToAction("ListUsers","Admin");
                    }                    
                    //否则就是登录当前注册用户并重定向到HomeController的 
                    //Index()操作方法中
                    await  _signInManager.SignInAsync(user,isPersistent:false);
                    return RedirectToAction("index","home");
                }

                ViewBag.ErrorTitle = "注册成功";
                ViewBag.ErrorMessage = $"在您登入系统前，我们已经给您发了一份邮件，需要您先进行邮件验证，单击确认链接即可完成";

                //如果有任何错误，则将它们添加到ModelState对象中
                //将由验证摘要标记助手显示到视图中
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }
        #endregion

        #region 注销
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("index", "home");
        }
        #endregion

        [AcceptVerbs("Get","Post")]
        public async Task<IActionResult> IsEmailInUse(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Json(true);
            }
            else
            {
                return Json($"邮箱：{email} 已经被注册使用了。");
            }
        }

        #region 验证邮箱
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("index", "home");
            }

            var user = await _userManager.FindByNameAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"当前{ userId}无效";
                return View("NotFound");
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return View();
            }

            ViewBag.ErrorTitle = "您的电子邮箱还未进行验证";
            return View("Error");
        }
        #endregion

        #region 激活邮箱
        [HttpGet]
        public IActionResult ActivateUserEmail()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ActivateUserEmail(EmailAddressViewModel model)
        {
            if (!ModelState.IsValid)
            {
                //通过邮箱地址查询用户地址
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user != null)
                {
                    if (!await _userManager.IsEmailConfirmedAsync(user))
                    {
                        //生成电子邮箱确认令牌
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        // 生成电子邮箱的确认链接
                        var confirmationLink = Url.Action("ConfirmEmail", "Account",
                            new { userId = user.Id, token = token }, Request.Scheme);
                        //记录生成的URL链接
                        _logger.Log(LogLevel.Warning, confirmationLink);

                        ViewBag.Message = "如果您在我们系统有注册账户，我们已经发了邮件到您的邮箱中，请前往邮箱激活您的账户。";
                        //重定向到忘记邮箱确认视图
                        return View("ActivateUserEmailConfirmation", ViewBag.Message);
                    }
                }
            }
            ViewBag.Message = "请确认邮箱是否存在异常，现在我们无法给您发送激活链接。";
            // 为了避免账户枚举和暴力攻击，不进行用户不存在或邮箱未验证的提示
            return View("ActivateUserEmailConfirmation",ViewBag.Message);
        }
        #endregion

        #region 忘记密码
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        public async Task<IActionResult> ForgotPassword(EmailAddressViewModel model)
        {
            if (ModelState.IsValid)
            {
                //通过邮箱地址查询用户地址
                var user = await _userManager.FindByEmailAsync(model.Email);
                
                //如果找到了用户并且确认了电子邮箱
                if (user != null && await _userManager.IsEmailConfirmedAsync(user))
                {
                    //生成重置密码令牌
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    // 生成密码重置链接
                    var passwordLink = Url.Action("ResetPassword", "Account",
                        new { email = model.Email, token = token }, Request.Scheme);
                    //记录生成的URL链接
                    _logger.Log(LogLevel.Warning, passwordLink);

                    //重定向用户到忘记密码确认试图
                    return View("ForgotPasswordConfirmation");
                }

                //为了避免账户枚举和暴力攻击，不进行用户不存在或邮箱未验证的提示
                return View("ForgotPasswordConfirmation");
            }

            return View(model);
        }
        #endregion

        #region 重置密码
        [HttpGet]
        public IActionResult ResetPassword(string token ,string email)
        {
            //如果密码重置令牌或电子邮箱为空，则有可能是用户在试图篡改密码重置链接
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "无效的密码重置令牌");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                //通过邮箱地址查找用户
                var user = await _userManager.FindByEmailAsync(model.Email);
                
                if (user != null)
                {
                    //重置用户密码
                    var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
                    if (result.Succeeded)
                    {
                        return View("ResetPasswordConfirmation");
                    }

                    //显示验证错误信息。当密码重置令牌已用或密码复杂性
                    //规则不符合标准时，触发行为
                    foreach (var error in result.Errors)
                    { 
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }

                // 为了避免账户枚举和暴力攻击，不要提示用户不存在
                return View("ResetPasswordConfirmation");
            }

            // 如果模型验证未通过，则显示验证错误
            return View(model);
        }
        #endregion
    }
}
