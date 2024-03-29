﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace MockSchoolManagement.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string City { get; set; }

        public IList<Claim> Claims { get; set; }

        public IList<string> Roles { get; set; }

        public EditUserViewModel()
        {
            Claims = new List<Claim>();
            Roles = new List<string>();
        }
    }
}
