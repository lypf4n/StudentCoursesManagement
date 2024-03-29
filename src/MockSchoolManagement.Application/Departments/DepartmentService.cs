﻿using Microsoft.EntityFrameworkCore;
using MockSchoolManagement.Application.Departments.Dtos;
using MockSchoolManagement.Application.Dtos;
using MockSchoolManagement.Infrastructure.Repositories;
using MockSchoolManagement.Models;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace MockSchoolManagement.Application.Departments
{
    public class DepartmentService : IDepartmentsService
    {
        private readonly IRepository<Department, int> _departmentsRepository;

        public DepartmentService(IRepository<Department, int> departmentsRepository)
        {
            _departmentsRepository = departmentsRepository;
        }

        public async Task<PagedResultDto<Department>> GetPagedDepartmentList(GetDepartmentInput input)
        {
            var query = _departmentsRepository.GetAll();

            if(!string.IsNullOrEmpty(input.FilterText))
            {
                query = query.Where(s => s.Name.Contains(input.FilterText));
            }

            //统计查询数据的总条数，用于分页计算总页数
            var count = query.Count();
            //根据需求进行排序，进行分页逻辑的计算
            query = query.OrderBy(input.Sorting).Skip((input.CurrentPage - 1) * input.MaxResultCount)
                .Take(input.MaxResultCount);
            //将查询结果转换为List集合，加载到内存中
            var models = await query.Include(a => a.Administrator).AsNoTracking().ToListAsync();

            var dtos = new PagedResultDto<Department>
            {
                TotalCount = count,
                CurrentPage = input.CurrentPage,
                MaxResultCount = input.MaxResultCount,
                Data = models,
                FilterText = input.FilterText,
                Sorting = input.Sorting
            };
            return dtos;
        }
    }
}
