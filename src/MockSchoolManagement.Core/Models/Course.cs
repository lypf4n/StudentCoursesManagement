﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MockSchoolManagement.Models
{
    public class Course
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Display(Name = "课程编号")]
        public int CourseID { get; set; }

        [Display(Name = "课程名称")]
        public string Title { get; set; }

        [Display(Name = "课程学分")]
        [Range(0, 5)]
        public int Credits { get; set; }


        public int DepartmentID { get; set; }
        public Department Department { get; set; }
        public ICollection<CourseAssignment> CourseAssignments { get; set; }

        public ICollection<StudentCourse> StudentCourses { get; set; }
    }
}
