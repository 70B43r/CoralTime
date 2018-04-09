﻿using CoralTime.Common.Exceptions;
using CoralTime.Common.Helpers;
using CoralTime.DAL.ConvertModelToView;
using CoralTime.DAL.Models;
using CoralTime.ViewModels.Reports;
using CoralTime.ViewModels.Reports.Request.Grid;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using static CoralTime.Common.Constants.Constants;

namespace CoralTime.BL.Services.Reports.DropDownsAndGrid
{
    public partial class ReportsService
    {
        #region Get DropDowns and Grid. Filtration By / Grouping By: Projects, Users, Dates, Clients.

        public ReportTotalView GetReportsGrid(ReportsGridView reportsGridView)
        {
            CheckAndSaveCurrentQuery(reportsGridView);

            var groupById = reportsGridView.CurrentQuery.GroupById;
            var showColumnIds = reportsGridView.CurrentQuery.ShowColumnIds;

            var dateFrom = reportsGridView.CurrentQuery.DateFrom;
            var dateTo = reportsGridView.CurrentQuery.DateTo;

            var dateFormatId = reportsGridView.DateFormatId;

            var reportTotalView = new ReportTotalView(groupById, showColumnIds, dateFormatId, dateFrom, dateTo);

            var filteredTimeEntries = GetFilteredTimeEntries(reportsGridView);
            if (filteredTimeEntries.Any())
            {
                switch (reportsGridView.CurrentQuery.GroupById)
                {
                    case (int) ReportsGroupByIds.Project:
                    {
                        var timeEntriesGroupByProjects = filteredTimeEntries
                            .GroupBy(i => i.Project)
                            .OrderBy(x => x.Key.Name)
                            .ToDictionary(key => key.Key, value => value.OrderBy(x => x.Date).ToList());

                        return reportTotalView.GetView(timeEntriesGroupByProjects);
                    }

                    case (int) ReportsGroupByIds.Member:
                    {
                        var timeEntriesGroupByMembers = filteredTimeEntries
                            .GroupBy(i => i.Member)
                            .OrderBy(x => x.Key.FullName)
                            .ToDictionary(key => key.Key, value => value.OrderBy(x => x.Date).ToList());

                        return reportTotalView.GetView(timeEntriesGroupByMembers);
                    }

                    case (int) ReportsGroupByIds.Date:
                    {
                        var timeEntriesGroupByDate = filteredTimeEntries
                            .GroupBy(i => i.Date)
                            .OrderBy(x => x.Key)
                            .ToDictionary(key => key.Key, key => key.OrderBy(x => x.Date).ToList());

                        return reportTotalView.GetView(timeEntriesGroupByDate);
                    }

                    case (int) ReportsGroupByIds.Client:
                    {
                        var timeEntriesGroupByClients = filteredTimeEntries
                            .GroupBy(i => i.Project.Client == null ? CreateWithOutClientInstance() : i.Project.Client)
                            .OrderBy(x => x.Key.Name)
                            .ToDictionary(key => key.Key, value => value.OrderBy(x => x.Date).ToList());

                        return reportTotalView.GetView(timeEntriesGroupByClients);
                    }
                }
            }

            return reportTotalView;
        }

        private void CheckAndSaveCurrentQuery(ReportsGridView reportsGridView)
        {
            var currentQuery = reportsGridView.CurrentQuery;
            var currentQueryFromCache = _reportsSettingsService.GetCurrentOrDefaultQuery();

            // TODO realize comparing two objects!
            if (currentQuery.DateStaticId != currentQueryFromCache.DateStaticId ||
                currentQuery.DateFrom != currentQueryFromCache.DateFrom ||
                currentQuery.DateTo != currentQueryFromCache.DateTo ||
                currentQuery.ClientIds != currentQueryFromCache.ClientIds ||
                currentQuery.GroupById != currentQueryFromCache.GroupById ||
                currentQuery.MemberIds != currentQueryFromCache.MemberIds ||
                currentQuery.ProjectIds != currentQueryFromCache.ProjectIds ||
                currentQuery.QueryId != currentQueryFromCache.QueryId || //TODO need?
                currentQuery.QueryName != currentQueryFromCache.QueryName ||
                currentQuery.ShowColumnIds != currentQueryFromCache.ShowColumnIds)
            {
                _reportsSettingsService.SaveCurrentQuery(currentQuery);

                reportsGridView.CurrentQuery = currentQuery;
            }
        }

        #endregion

        #region Get DropDowns and Grid. Filtration By / Grouping By: Projects, Users, Dates, Clients. (Common methods)

        private List<TimeEntry> GetFilteredTimeEntries(ReportsGridView reportsGridView)
        {
            var currentMember = MemberImpersonated; // Uow.MemberRepository.LinkedCacheGetByName(ImpersonatedUserName);

            DateTime dateFrom = new DateTime();
            DateTime dateTo = new DateTime();

            FillDatesByDateStaticOrDateFromTo(reportsGridView, currentMember, ref dateFrom, ref dateTo);

            // By Dates (default grouping, i.e. "Group by None"; direct order).
            var timeEntriesByDateOfUser = GetTimeEntryByDate(currentMember, dateFrom, dateTo);

            // By Projects.
            if (reportsGridView.CurrentQuery?.ProjectIds != null && reportsGridView.CurrentQuery.ProjectIds.Length > 0)
            {
                CheckAndSetIfInFilterChooseSingleProject(reportsGridView, timeEntriesByDateOfUser);

                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridView.CurrentQuery.ProjectIds.Contains(x.ProjectId));
            }

            // By Members.
            if (reportsGridView.CurrentQuery?.MemberIds != null && reportsGridView.CurrentQuery.MemberIds.Length > 0)
            {
                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridView.CurrentQuery.MemberIds.Contains(x.MemberId));
            }

            // By Clients that has Projects.
            if (reportsGridView.CurrentQuery?.ClientIds != null && reportsGridView.CurrentQuery.ClientIds.Length > 0)
            {
                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridView.CurrentQuery.ClientIds.Contains(x.Project.ClientId) || x.Project.ClientId == null && reportsGridView.CurrentQuery.ClientIds.Contains(WithoutClient.Id));
            }

            return timeEntriesByDateOfUser.ToList();
        }

        private void FillDatesByDateStaticOrDateFromTo(ReportsGridView reportsGridView, Member currentMember, ref DateTime dateFrom, ref DateTime dateTo)
        {
            var dateStaticId = reportsGridView.CurrentQuery.DateStaticId;
            var isFilledOnlyDateStaticId = dateStaticId != null && reportsGridView.CurrentQuery?.DateFrom == null && reportsGridView.CurrentQuery?.DateTo == null;
            var isFilledOnlyDateFromDateTo = dateStaticId == null && reportsGridView.CurrentQuery?.DateFrom != null && reportsGridView.CurrentQuery?.DateTo != null;

            if (!isFilledOnlyDateStaticId && !isFilledOnlyDateFromDateTo)
            {
                throw new CoralTimeDangerException("Wrong input conditional: to get entities by DateStaticId or Date From/To properties.");
            }

            if (isFilledOnlyDateStaticId)
            {
                var today = DateTime.Today.Date;

                switch (dateStaticId)
                {
                    case (int) DatesStaticIds.Today:
                    {
                        dateFrom = dateTo = today;
                        break;
                    }

                    case (int) DatesStaticIds.ThisWeek:
                    {
                        var memberDayOfWeekStart = currentMember.WeekStart == WeekStart.Monday
                            ? DayOfWeek.Monday
                            : DayOfWeek.Sunday;

                        CommonHelpers.SetRangeOfThisWeekByDate(out var thisWeekStart, out var thisWeekEnd, DateTime.Now.Date, memberDayOfWeekStart);

                        dateFrom = thisWeekStart;
                        dateTo = thisWeekEnd;

                        break;
                    }

                    case (int) DatesStaticIds.ThisMonth:
                    {
                        CommonHelpers.SetRangeOfThisMonthByDate(out var thisMonthByTodayFirstDate, out var thisMonthByTodayLastDate, today);

                        dateFrom = thisMonthByTodayFirstDate;
                        dateTo = thisMonthByTodayLastDate;

                        break;
                    }

                    case (int) DatesStaticIds.ThisYear:
                    {
                        CommonHelpers.SetRangeOfThisYearByDate(out var thisYearByTodayFirstDate, out var thisYearByTodayLastDate, today);

                        dateFrom = thisYearByTodayFirstDate;
                        dateTo = thisYearByTodayLastDate;

                        break;
                    }

                    case (int) DatesStaticIds.Yesterday:
                    {
                        var yesterday = DateTime.Today.Date.AddMilliseconds(-1);

                        dateFrom = dateTo = yesterday;

                        break;
                    }

                    case (int) DatesStaticIds.LastWeek:
                    {
                        var memberDayOfWeekStart = currentMember.WeekStart == WeekStart.Monday
                            ? DayOfWeek.Monday
                            : DayOfWeek.Sunday;

                        CommonHelpers.SetRangeOfLastWeekByDate(out var lastWeekStart, out var lastWeekEnd, DateTime.Now.Date,
                            memberDayOfWeekStart);

                        dateFrom = lastWeekStart;
                        dateTo = lastWeekEnd;

                        break;
                    }

                    case (int) DatesStaticIds.LastMonth:
                    {
                        CommonHelpers.SetRangeOfLastMonthByDate(out var lastMonthByTodayFirstDate, out var lastMonthByTodayLastDate, today);

                        dateFrom = lastMonthByTodayFirstDate;
                        dateTo = lastMonthByTodayLastDate;

                        break;
                    }

                    case (int) DatesStaticIds.LastYear:
                    {
                        CommonHelpers.SetRangeOfLastYearByDate(out var lastYearByTodayFirstDate, out var lastYearByTodayLastDate, today);

                        dateFrom = lastYearByTodayFirstDate;
                        dateTo = lastYearByTodayLastDate;

                        break;
                    }
                }
            }

            if (isFilledOnlyDateFromDateTo)
            {
                dateFrom = (DateTime) reportsGridView.CurrentQuery?.DateFrom;
                dateTo = (DateTime) reportsGridView.CurrentQuery?.DateTo;
            }
        }

        private void CheckAndSetIfInFilterChooseSingleProject(ReportsGridView reportsGridData, IQueryable<TimeEntry> timeEntriesByDateOfUser)
        {
            if (reportsGridData.CurrentQuery.ProjectIds.Length == 1)
            {
                var singleFilteredProjectId = reportsGridData.CurrentQuery.ProjectIds.FirstOrDefault();
                SingleFilteredProjectName = Uow.ProjectRepository.LinkedCacheGetById(singleFilteredProjectId).Name;
            }
        }

        private IQueryable<TimeEntry> GetTimeEntryByDate(Member currentMember, DateTime dateFrom, DateTime dateTo)
        {
            // #0 Get timeEntriesByDate.s
            var timeEntriesByDate = Uow.TimeEntryRepository.GetQueryWithIncludes()
                .Include(x => x.Project).ThenInclude(x => x.Client)
                .Include(x => x.Member.User)
                .Include(x => x.TaskType)
                .Where(t => t.Date.Date >= dateFrom.Date && t.Date.Date <= dateTo.Date);

            #region Constrain for Admin: return all TimeEntries.

            if (currentMember.User.IsAdmin)
            {
                return timeEntriesByDate;
            }

            #endregion

            #region Constrain for Member. return only TimeEntries that manager is assign.

            if (!currentMember.User.IsAdmin && !currentMember.User.IsManager)
            {
                // #1. TimeEntries. Get tEntries for this member.
                timeEntriesByDate = timeEntriesByDate.Where(t => t.MemberId == currentMember.Id);
            }

            #endregion

            #region Constrain for Manager : return #1 TimeEntries that currentMember is assign, #2 TimeEntries for not assign users at Projects (but TEntries was saved), #4 TimeEntries with global projects that not contains in result.

            if (!currentMember.User.IsAdmin && currentMember.User.IsManager)
            {
                var managerRoleId = Uow.ProjectRoleRepository.LinkedCacheGetList().FirstOrDefault(r => r.Name == ProjectRoleManager).Id;

                var managerProjectIds = Uow.MemberProjectRoleRepository.LinkedCacheGetList()
                    .Where(r => r.MemberId == currentMember.Id && r.RoleId == managerRoleId)
                    .Select(x => x.ProjectId)
                    .ToArray();

                // #1. TimeEntries. Get tEntries for this member and tEntries that is current member is Manager!.
                timeEntriesByDate = timeEntriesByDate.Where(t => t.MemberId == currentMember.Id || managerProjectIds.Contains(t.ProjectId));
            }

            return timeEntriesByDate;

            #endregion
        }

        private Client CreateWithOutClientInstance()
        {
            return new Client
            {
                Id = WithoutClient.Id,
                Name = WithoutClient.Name,
                CreationDate = DateTime.Now,
                LastUpdateDate = DateTime.Now,
            };
        }

        #endregion
    }
}