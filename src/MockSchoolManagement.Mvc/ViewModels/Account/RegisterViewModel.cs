﻿using Microsoft.AspNetCore.Mvc;
using MockSchoolManagement.CustomerMiddlewares.Utils;
using System.ComponentModel.DataAnnotations;

namespace MockSchoolManagement.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [ValidEmailDomain(allowedDomain:"52abp.com",
            ErrorMessage = "邮箱地址的后缀必须是52abp.com")]
        [Remote(action:"IsEmailInUse",controller:"Account")]
        [Display(Name ="邮箱地址")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "确认密码")]
        [Compare("Password", ErrorMessage = "密码与确认密码不一致，请重新输入.")]
        public string ConfirmPassword { get; set; }

        public string City { get; set; }
    }
}
