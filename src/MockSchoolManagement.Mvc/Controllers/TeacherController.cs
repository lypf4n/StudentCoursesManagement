﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockSchoolManagement.Application.Teachers;
using MockSchoolManagement.Application.Teachers.Dtos;
using MockSchoolManagement.Infrastructure.Repositories;
using MockSchoolManagement.Models;
using MockSchoolManagement.ViewModels;
using MockSchoolManagement.ViewModels.Teacher;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MockSchoolManagement.Controllers
{
    public class TeacherController : Controller
    {
        private readonly ITeacherService _teacherService;
        private readonly IRepository<Teacher, int> _teacherRepository;
        private readonly IRepository<Course, int> _courseRepository;
        private readonly IRepository<OfficeLocation, int> _officeLocationRepository;
        private readonly IRepository<CourseAssignment, int> _courseAssignmentRepository;


        public TeacherController(ITeacherService teacherService,
            IRepository<Teacher, int> teacherRepository,
            IRepository<Course, int> courseRepository,
            IRepository<OfficeLocation, int> officeLocationRepository,
            IRepository<CourseAssignment, int> courseAssignmentRepository)
        {
            _teacherService = teacherService;
            _teacherRepository = teacherRepository;
            _courseRepository = courseRepository;
            _officeLocationRepository = officeLocationRepository;
            _courseAssignmentRepository = courseAssignmentRepository;
        }

        #region 教师列表页
        public async Task<IActionResult> Index(GetTeacherInput input)
        {
            var models = await _teacherService.GetPagedTeacherList(input);
            var dto = new TeacherListViewModel();
            if (input.Id != null)
            {
                //查询教师教授的课程列表
                var teacher = models.Data.FirstOrDefault(a => a.Id == input.Id.Value);
                if (teacher != null)
                {
                    dto.Courses = teacher.CourseAssignments.Select(a => a.Course).ToList();
                }
                dto.SelectedId = input.Id.Value;
            }

            if (input.CourseId.HasValue)
            {
                //查询该课程下有多少学生报名
                var course = dto.Courses.FirstOrDefault(a => a.CourseID == input.CourseId.Value);
                if (course != null)
                {
                    dto.StudentCourses = course.StudentCourses.ToList();
                }
                dto.SelectedCourseId = input.CourseId.Value;
            }

            dto.Teachers = models;

            return View(dto);
        }
        #endregion


        #region 编辑教师
        
        public async Task<IActionResult> Edit(int? id)
        {
            var model = await _teacherRepository.GetAll().Include(a => a.OfficeLocation)
                .Include(a => a.CourseAssignments).ThenInclude(a => a.Course)
                .AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

            if (model == null)
            {
                ViewBag.ErrorMessage = $"教师信息ID为{id}的信息不存在，请重试。";
                return View("NotFound");
            }

            //处理业务的视图模型
            var dto = new TeacherCreateViewModel
            {
                Name = model.Name,
                Id = model.Id,
                HireDate = model.HireDate,
                OfficeLocation = model.OfficeLocation
            };

            //从课程列表中处理哪些课程已经分配哪些未分配
            var assignedCourses = AssignedCourseDroupDownList(model);
            dto.AssignedCourses = assignedCourses;

            return View(dto);
        }

        /// <summary>
        /// 判断课程是否被选中
        /// </summary>
        /// <returns></returns>
        private List<AssignedCourseViewModel> AssignedCourseDroupDownList(Teacher teacher)
        {
            //获取课程列表
            var allCourses = _courseRepository.GetAllList();
            //获取教师当前教授的课程
            var teacherCourses = new HashSet<int>(teacher.CourseAssignments.Select(c => c.CourseID));

            var viewModel = new List<AssignedCourseViewModel>();
            foreach (var course in allCourses)
            {
                viewModel.Add(new AssignedCourseViewModel
                { 
                    CourseID = course.CourseID,
                    Title = course.Title,
                    //将当前正在教授的课程设置为选中状态
                    IsSelected = teacherCourses.Contains(course.CourseID)
                });
            }

            return viewModel;
        }

        [HttpPost, ActionName("Edit")]
        public async Task<IActionResult> EditPost(TeacherCreateViewModel input)
        {
            if (ModelState.IsValid)
            {
                var teacher = await _teacherRepository.GetAll().Include(i => i.OfficeLocation)
                    .Include(i => i.CourseAssignments).ThenInclude(i => i.Course)
                    .FirstOrDefaultAsync(m => m.Id == input.Id);

                if (teacher == null)
                {
                    ViewBag.ErrorMessage = $"教师信息ID为{input.Id}的信息不存在，请重试。"; 
                    return View("NotFound");
                }

                teacher.HireDate = input.HireDate;
                teacher.Name = input.Name;
                teacher.OfficeLocation = input.OfficeLocation;
                teacher.CourseAssignments = new List<CourseAssignment>();

                //从试图中获取被选中的课程信息
                var courses = input.AssignedCourses.Where(a => a.IsSelected == true).ToList();

                foreach (var item in courses)
                {
                    //将选中的课程信息赋值到导航属性CourseAssignments中
                    teacher.CourseAssignments.Add(new CourseAssignment { CourseID = item.CourseID, TeacherID = teacher.Id });
                }
                await _teacherRepository.UpdateAsync(teacher);
                return RedirectToAction(nameof(Index));
            }

            return View(input);
        }
        #endregion


        #region 添加教师信息

        public IActionResult Create()
        {
            var allCourses = _courseRepository.GetAllList();
            var viewModel = new List<AssignedCourseViewModel>();
            foreach (var course in allCourses)
            {
                viewModel.Add(new AssignedCourseViewModel
                {
                    CourseID = course.CourseID,
                    Title = course.Title,
                    IsSelected = false
                });
            }

            var dto = new TeacherCreateViewModel();
            dto.AssignedCourses = viewModel;

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create(TeacherCreateViewModel input)
        {
            if (ModelState.IsValid)
            {
                var teacher = new Teacher
                {
                    HireDate = input.HireDate,
                    Name = input.Name,
                    OfficeLocation = input.OfficeLocation,
                    CourseAssignments = new List<CourseAssignment>()
                };

                //获取用户选中的课程信息
                var courses = input.AssignedCourses.Where(a => a.IsSelected == true).ToList();
                foreach (var item in courses)
                {
                    //将选中的课程西悉尼添加到导航属性
                    teacher.CourseAssignments.Add(new CourseAssignment { CourseID = item.CourseID, TeacherID = teacher.Id });
                }
                await _teacherRepository.InsertAsync(teacher);
                return RedirectToAction(nameof(Index));
            }
            return View(input);
        }
        #endregion


        #region 删除教师信息

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var model = await _teacherRepository.FirstOrDefaultAsync(a => a.Id == id);

            if (model == null)
            {
                ViewBag.ErrorMessage = $"教师id为{id}的信息不存在，请重试。";
                return View("NotFound");
            }

            await _officeLocationRepository.DeleteAsync(a => a.TeacherId == model.Id);
            await _courseAssignmentRepository.DeleteAsync(a => a.TeacherID == model.Id);

            await _teacherRepository.DeleteAsync(a => a.Id == id);

            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
