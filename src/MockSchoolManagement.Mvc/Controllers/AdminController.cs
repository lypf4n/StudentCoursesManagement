﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MockSchoolManagement.Models;
using MockSchoolManagement.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MockSchoolManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;


        public AdminController(RoleManager<IdentityRole> roleManger,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _roleManger = roleManger;
            _userManager = userManager;
            _logger = logger;
        }


        #region 创建角色
        [HttpGet]
        public IActionResult CreateRole()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(CreateRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                //我们只需要指定一个不重复的角色名称来创建新角色
                IdentityRole identityRole = new IdentityRole
                { 
                    Name = model.RoleName
                };
                
                //将角色保存再AspNetRoles表中
                IdentityResult result = await _roleManger.CreateAsync(identityRole);

                if (result.Succeeded)
                {
                    return RedirectToAction("ListRoles", "Admin");
                }

                foreach(IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }
        #endregion

        
        [HttpGet]
        public IActionResult ListRoles()
        {
            var roles = _roleManger.Roles;
            return View(roles);
        }

        #region 编辑角色
        /// <summary>
        /// 角色ID从URL传递给操作方法
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> EditRole(string id)
        {
            // 通过角色ID查找角色
            var role = await _roleManger.FindByIdAsync(id);

            if (role == null)
            {
                ViewBag.ErrorMessage = $"角色Id={id}的信息不存在，请重试.";
                return View("NotFound");
            }

            var model = new EditRoleViewModel
            {
                Id = role.Id,
                RoleName = role.Name
            };
            var users = _userManager.Users.ToList();

            //查询所有的用户
            foreach (var user in users)
            {
                //如果角色拥有此角色，请将用户添加到
                //EditRoleViewModel的Users属性中
                //然后将对象传递给试图显示到客户端
                if (await _userManager.IsInRoleAsync(user, role.Name))
                {
                    model.Users.Add(user.UserName);
                }
            }

            return View(model);
        }

        /// <summary>
        /// 此操作方法用于响应HttpPOST的请求并接收EditRoleViewModel模型数据
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> EidtRole(EditRoleViewModel model)
        {
            var role = await _roleManger.FindByIdAsync(model.Id);

            if (role == null)
            {
                ViewBag.ErrorMessage = $"角色Id={model.Id}的信息不存在，请重试.";
                return View("NotFound");
            }
            else
            {
                role.Name = model.RoleName;

                //使用UpdateAsync更新角色
                var result = await _roleManger.UpdateAsync(role);

                if (result.Succeeded)
                {
                    return RedirectToAction("ListRoles");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }
        }
        #endregion

        #region 编辑用户角色
        public async Task<IActionResult> EditUsersInRole(string roleId)
        {
            ViewBag.roleId = roleId;
            // 通过roleId查询角色实体信息
            var role = await _roleManger.FindByIdAsync(roleId);
            if (role == null)
            {
                ViewBag.ErrorMessage = $"角色Id={roleId}的信息不存在，请重试。";
                return View("NotFound");
            }
            var model = new List<UserRoleViewModel>();
            var users = _userManager.Users.ToList();
            foreach (var user in users)
            {
                var userRoleViewModel = new UserRoleViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName
                };
                // 判断当前用户是否已经存在与角色中
                var isInRole = await _userManager.IsInRoleAsync(user, role.Name);
                if (isInRole)
                {
                    //存在则设置为选中状态，值为true
                    userRoleViewModel.IsSelected = true;
                }
                else 
                {
                    //不存在则设置为非选中状态，值为false
                    userRoleViewModel.IsSelected = false;
                }
                model.Add(userRoleViewModel);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUsersInRole(List<UserRoleViewModel> model, string roleId)
        {
            var role = await _roleManger.FindByIdAsync(roleId);
            // 检查当前角色是否存在
            if (role == null)
            {
                ViewBag.ErrorMessage = $"角色Id={roleId}的信息不存在，请重试。"; 
                return View("NotFound");
            }

            for (var i = 0; i < model.Count; i++)
            {
                var user = await _userManager.FindByIdAsync(model[i].UserId);
                IdentityResult result = null;
                //检查当前的userId是否被选中，如果被选中了，则添加到角色列表中
                if (model[i].IsSelected && !(await _userManager.IsInRoleAsync(user, role.Name)))
                {
                    result = await _userManager.AddToRoleAsync(user, role.Name);
                }
                //如果没有选中，则从userroles表中移除
                else if (!model[i].IsSelected && (await _userManager.IsInRoleAsync(user, role.Name)))
                {
                    result = await _userManager.RemoveFromRoleAsync(user, role.Name);

                }
                //对于其他情况不做处理，继续新的循环
                else
                {
                    continue;
                }

                if (result.Succeeded)
                {
                    //判断当前用户是否为最后一个用户，如果是，则跳转回EditRole视图；
                    //如果不是，则进入下一个循环
                    if (i < model.Count - 1)
                        continue;
                    else
                        return RedirectToAction("EditRole", new {Id = roleId});
                }
            }

            return RedirectToAction("EditRole", new { Id = roleId });
        }
        #endregion

        #region 删除角色
        [HttpPost]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManger.FindByIdAsync(id);

            if (role == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{id}的角色信息";
                return View("NotFound");
            }
            else
            {
                //将代码包装再try catch中
                try
                {
                    var result = await _roleManger.DeleteAsync(role);

                    if (result.Succeeded)
                    {
                        return RedirectToAction("ListRoles");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }

                    return View("ListRoles");
                }
                //如果触发的异常是DbUpdateException，则知道我们无法删除角色
                //因为该角色中已存在用户信息
                catch (DbUpdateException ex)
                {
                    //将异常记录到日志文件中。我们之前已经学习使用NLog配置日志信息
                    _logger.LogError($"发生异常 :{ex}");
                    //我们使用ViewBag.ErrorTitle和ViewBag.ErrorMessage来传递
                    //错误标题和详情信息到错误视图
                    //错误视图会将这些数据显示给用户
                    ViewBag.ErrorTitle = $"角色:{role.Name} 正在被使用中...";
                    ViewBag.ErrorMessage = $" 无法删除{role.Name}角色，因为此角色中已经存在用户。如果读者想删除此角色，需要先从该角色中删除用户，然后尝试删除该角色本身。";

                    return View("Error");
                }


            }
        }
        #endregion

        #region 用户管理        
        [HttpGet]public IActionResult ListUsers()
        {            
            var users = _userManager.Users.ToList();
            return View(users);
        }
        #endregion

        #region 编辑用户
        [HttpGet]
        [Authorize(Policy = "EditRolePolicy")]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id); 
            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{id}的用户";
                return View("NotFound");
            }

            // GetClaimsAsync返回用户声明列表
            var userClaims = await _userManager.GetClaimsAsync(user);
            // 返回用户角色列表
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                City = user.City,
                Claims = userClaims,
                Roles = userRoles
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "EditRolePolicy")]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{model.Id}的用户";
                return View("NotFound");
            }
            else
            {
                user.Email = model.Email;
                user.UserName = model.UserName;
                user.City = model.City;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return RedirectToAction("ListUsers");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }
        }
        #endregion

        #region 删除用户
        [HttpPost]
        [Authorize(Policy = "SuperAdminPolicy")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{id}的用户";
                return View("NotFound");
            }
            else
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction("ListUsers");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View("ListUsers");
            }
        }
        #endregion

        #region 管理用户的角色
        [HttpGet]
        public async Task<IActionResult> ManageUserRoles(string userId)
        {
            ViewBag.userId = userId;

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            { 
                ViewBag.ErrorMessage = $"无法找到ID为{userId}的用户";
                return View("NotFound");
            }

            var model = new List<RolesInUserViewModel>();

            var roles = await  _roleManger.Roles.ToListAsync();
            foreach (var role in roles)
            {
                var rolesInUserViewModel = new RolesInUserViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.Name
                };
                // 判断当前用户是否已经拥有该角色信息
                if (await _userManager.IsInRoleAsync(user, role.Name))
                {
                    //将已拥有的角色设置为选中
                    rolesInUserViewModel.IsSelected = true;
                }
                else
                {
                    rolesInUserViewModel.IsSelected = false;
                }
                //添加已有角色到试图模型列表
                model.Add(rolesInUserViewModel);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ManageUserRoles(List<RolesInUserViewModel> model, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{userId}的用户";
                return View("NotFound");
            }

            var roles = await _userManager.GetRolesAsync(user);
            //移除当前用户中的所有角色信息
            var result = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "无法删除用户中的现有角色");
                return View(model);
            }

            //查询模型列表中被选中的RoleName并添加到用户中
            result = await _userManager.AddToRolesAsync(user,
                model.Where(x => x.IsSelected).Select(y => y.RoleName));
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "无法删除用户中的现有角色");
                return View(model);
            }

            return RedirectToAction("EditUser", new {Id = userId});
        }
        #endregion

        #region 管理用户的声明
        [HttpGet]
        public async Task<IActionResult> ManageUserClaims(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{userId}的用户";
                return View("NotFound");
            }

            //UserManager服务中的GetClaimsAsync()方法获取用户当前的所有声明
            var existingUserClaims = await _userManager.GetClaimsAsync(user);

            var model = new UserClaimsViewModel
            {
                UserId = userId
            };

            //循环遍历应用程序中的每个声明
            foreach (Claim claim in ClaimsStore.ALLClaims)
            {
                UserClaim userClaim = new UserClaim
                {
                    ClaimType = claim.Type
                };

                //如果用户选中了声明属性，则设置IsSelected属性为true
                if (existingUserClaims.Any(c => c.Type == claim.Type && c.Value == "true"))
                {
                    userClaim.IsSelected = true;
                }

                model.Claims.Add(userClaim);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ManageUserClaims(UserClaimsViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"无法找到ID为{model.UserId}的用户";
                return View("NotFound");
            }

            // 获取所有用户现有的声明并删除它们
            var claims = await _userManager.GetClaimsAsync(user);
            var result = await _userManager.RemoveClaimsAsync(user, claims);

            if (!result.Succeeded)
            { 
                ModelState.AddModelError("", "无法删除当前用户的声明");
                return View(model);
            }

            //添加Claim列表到数据库中，然后对UI界面上被选中的值进行bool判断
            result = await _userManager.AddClaimsAsync(user,
                model.Claims.Select(c => new Claim(c.ClaimType, c.IsSelected?"true":"false")));

            if (!result.Succeeded)
            {
                ModelState.AddModelError("","无法向用户添加选定的声明");
                return View(model);
            }

            return RedirectToAction("EditUser", new { Id = model.UserId });
        }
        #endregion

        #region 拒绝访问控制器
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        #endregion
    }
}
