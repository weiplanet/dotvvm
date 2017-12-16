﻿using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Controls;
using DotVVM.Samples.BasicSamples.Api.AspNetCore.DataStore;
using DotVVM.Samples.BasicSamples.Api.AspNetCore.Model;
using Microsoft.AspNetCore.Mvc;

namespace DotVVM.Samples.BasicSamples.Api.AspNetCore.AspNetCore.Controllers
{
    [Route("api/[controller]")]
    public class CompaniesController : Controller
    {
        [HttpGet]
        public List<Company> Get()
        {
            lock (Database.Instance)
            {
                return Database.Instance.Companies;
            }
        }

        [HttpGet]
        [Route("sorted")]
        public GridViewDataSet<Company> GetWithSorting([FromQuery]SortingOptions sortingOptions)
        {
            lock (Database.Instance)
            {
                var dataSet = new GridViewDataSet<Company>()
                {
                    SortingOptions = sortingOptions
                };
                dataSet.LoadFromQueryable(Database.Instance.Companies.AsQueryable());
                return dataSet;
            }
        }

        [HttpGet]
        [Route("paged")]
        public GridViewDataSet<Company> GetWithPaging([FromQuery]PagingOptions pagingOptions)
        {
            lock (Database.Instance)
            {
                var dataSet = new GridViewDataSet<Company>()
                {
                    PagingOptions = pagingOptions
                };
                dataSet.LoadFromQueryable(Database.Instance.Companies.AsQueryable());
                return dataSet;
            }
        }

        [HttpGet]
        [Route("sortedandpaged")]
        public GridViewDataSet<Company> GetWithSortingAndPaging([FromQuery]SortingOptions sortingOptions, [FromQuery]PagingOptions pagingOptions)
        {
            lock (Database.Instance)
            {
                var dataSet = new GridViewDataSet<Company>()
                {
                    PagingOptions = pagingOptions,
                    SortingOptions = sortingOptions
                };
                dataSet.LoadFromQueryable(Database.Instance.Companies.AsQueryable());
                return dataSet;
            }
        }

    }
    
}
