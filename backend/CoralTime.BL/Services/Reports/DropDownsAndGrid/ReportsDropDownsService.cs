﻿using CoralTime.Common.Constants;
using CoralTime.DAL.ConvertModelToView;
using CoralTime.DAL.Models;
using CoralTime.ViewModels.Reports.Request.Grid;
using CoralTime.ViewModels.Reports.Responce.DropDowns.Filters;
using CoralTime.ViewModels.Reports.Responce.DropDowns.GroupBy;
using System.Collections.Generic;
using System.Linq;

namespace CoralTime.BL.Services.Reports.DropDownsAndGrid
{
    public partial class ReportsService
    {
        #region values of groupByInfo, showColumnsInfo, datesStaticInfo.

        private readonly ReportCommonDropDownsView[] groupByInfo =
        {
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ReportsGroupByIds.Project,
                Description = Constants.ReportsGroupByIds.Project.ToString()
            },

            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ReportsGroupByIds.Member,
                Description = Constants.ReportsGroupByIds.Member.ToString()
            },

            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ReportsGroupByIds.Date,
                Description = Constants.ReportsGroupByIds.Date.ToString()
            },

            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ReportsGroupByIds.Client,
                Description = Constants.ReportsGroupByIds.Client.ToString()
            }
        };

        private readonly ReportCommonDropDownsView[] showColumnsInfo =
        {
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ShowColumnModelIds.ShowEstimatedTime,
                Description = "Show Estimated Hours"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ShowColumnModelIds.ShowDate,
                Description = "Show Date"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ShowColumnModelIds.ShowNotes,
                Description = "Show Notes"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.ShowColumnModelIds.ShowStartFinish,
                Description = "Show Start/Finish Time"
            }
        };

        private readonly ReportCommonDropDownsView[] datesStaticInfo =
        {
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.Today,
                Description = "Today"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.ThisWeek,
                Description = "This Week"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.ThisMonth,
                Description = "This Month"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.ThisYear,
                Description = "This Year"
            },

            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.Yesterday,
                Description = "Yesterday"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.LastWeek,
                Description = "Last Week"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.LastMonth,
                Description = "Last Month"
            },
            new ReportCommonDropDownsView
            {
                Id = (int) Constants.DatesStaticIds.LastYear,
                Description = "Last Year"
            }
        };

        #endregion

        public ReportDropDownView GetReportsDropDowns()
        {
            var currentQuery = _reportsSettingsService.GetCurrentOrCreateDefaultQuery();

            var reportDropDowns = new ReportDropDownView
            {
                Values = CreateDropDownValues(MemberImpersonated),
                CurrentQuery = currentQuery
            };

            return reportDropDowns;
        }

        private ReportDropDownValues CreateDropDownValues(Member memberImpersonated)
        {
            var managerRoleId = Uow.ProjectRoleRepository.GetManagerRoleId();
            var memberRoleId = Uow.ProjectRoleRepository.GetMemberRoleId();

            var projects = new List<Project>();
            var clients = new List<Client>();
            var members = Uow.MemberRepository.LinkedCacheGetList();

            #region GetProjects allProjectsForAdmin or projectsWithAssignUsersAndPublicProjects.

            if (memberImpersonated.User.IsAdmin)
            {
                var allProjectsForAdmin = Uow.ProjectRepository.LinkedCacheGetList().ToList();
                projects = allProjectsForAdmin;
            }
            else
            {
                var projectsWithAssignUsersAndPublicProjects = Uow.ProjectRepository.LinkedCacheGetList()
                    .Where(x => x.MemberProjectRoles.Select(z => z.MemberId).Contains(memberImpersonated.Id) || !x.IsPrivate).ToList();

                projects = projectsWithAssignUsersAndPublicProjects;
            }

            #endregion

            #region Get Clients from Projects of clients.

            // 1. Get all clients from targeted projects where project is assign to client.
            var clientsWithProjects = projects.Where(project => project.Client != null)
                .Select(project => project.Client)
                .Distinct()
                .Select(client => new Client
                {
                    Id = client.Id,
                    Name = client.Name,
                    Email = client.Email,
                    IsActive = client.IsActive,
                    Description = client.Description,
                    Projects = new List<Project>(client.Projects.Where(projectOfClient => projects.Select(project => project.Id).Contains(projectOfClient.Id)).ToList())
                }).ToList();

            clients.AddRange(clientsWithProjects);

            // 2. Get all projects where project is not assign to client and create client "WithoutClients" that we add projects to it.
            var hasClientsWithoutProjects = projects.Where(x => x.Client == null).Any();
            if (hasClientsWithoutProjects)
            {
                var clientWithoutProjects = new Client
                {
                    Id = Constants.WithoutClient.Id,
                    Name = Constants.WithoutClient.Name,
                    IsActive = true,
                    Projects = new List<Project>(projects.Where(x => x.Client == null).ToList())
                };

                clients.Add(clientWithoutProjects);
            }

            #endregion

            var dropDownValues = new ReportDropDownValues
            {
                Filters = CreateReportClientViews(memberImpersonated, clients, managerRoleId, members, memberRoleId),
                GroupBy = groupByInfo,
                ShowColumns = showColumnsInfo,
                UserDetails = CreateUserDetails(memberImpersonated),
                CustomQueries = CreateValuesCustomQueries(memberImpersonated).OrderBy(x => x.QueryName).ToList(),
                DateStatic = datesStaticInfo
            };

            return dropDownValues;
        }

        private List<ReportsSettingsView> CreateValuesCustomQueries(Member memberImpersonated)
        {
            var valuesCustomQueries = new List<ReportsSettingsView>();

            var customQueries = Uow.ReportsSettingsRepository.LinkedCacheGetByMemberId(memberImpersonated.Id)
                .Where(x => x.QueryName != null);
            foreach (var reportSettings in customQueries)
            {
                var reportsSettingsView = reportSettings.GetView();

                valuesCustomQueries.Add(reportsSettingsView);
            }

            return valuesCustomQueries;
        }

        private List<ReportClientView> CreateReportClientViews(Member memberImpersonated, List<Client> clients, int managerRoleId, List<Member> members,
            int memberRoleId)
        {
            var reportClientView = new List<ReportClientView>();
            foreach (var client in clients)
            {
                var reportProjectViewByUserId = new List<ReportProjectView>();

                foreach (var project in client.Projects)
                {
                    var reportProjectView = new ReportProjectView
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name,
                        RoleId = project.MemberProjectRoles.FirstOrDefault(r => r.MemberId == memberImpersonated.Id)?.RoleId ??
                                 0,
                        IsProjectActive = project.IsActive,
                    };

                    #region Set all users at Project constrain only for: Admin, Manager at this project.

                    var isManagerOnProject =
                        project.MemberProjectRoles.Exists(r =>
                            r.MemberId == memberImpersonated.Id && r.RoleId == managerRoleId);

                    if (memberImpersonated.User.IsAdmin || isManagerOnProject)
                    {
                        var usersDetailsView = project.MemberProjectRoles
                            .Select(x => x.Member.GetViewReportUsers(x.RoleId, Mapper)).ToList();

                        // Add members, that is not assigned to this project directly.
                        if (!project.IsPrivate)
                        {
                            var notAssignedMembersAtProjView = members
                                .Where(x => project.MemberProjectRoles.Select(y => y.MemberId).All(mi => x.Id != mi))
                                .Select(u => u.GetViewReportUsers(memberRoleId, Mapper));
                            usersDetailsView.AddRange(notAssignedMembersAtProjView);
                        }

                        // Set all users of the project.
                        reportProjectView.UsersDetails = usersDetailsView;
                    }

                    #endregion

                    reportProjectView.IsUserManagerOnProject = isManagerOnProject;
                    reportProjectViewByUserId.Add(reportProjectView);
                }

                var reportClientViewlocal = new ReportClientView
                {
                    ClientId = client.Id,
                    ClientName = client.Name,
                    IsClientActive = client.IsActive,
                    ProjectsDetails = reportProjectViewByUserId
                };

                reportClientView.Add(reportClientViewlocal);
            }

            return reportClientView;
        }

        private ReportUserDetails CreateUserDetails(Member memberImpersonated)
        {
            var userDetails = new ReportUserDetails
            {
                CurrentUserFullName = memberImpersonated.FullName,
                CurrentUserId = memberImpersonated.Id,
                IsAdminCurrentUser = memberImpersonated.User.IsAdmin,
                IsManagerCurrentUser = memberImpersonated.User.IsManager,
            };
            return userDetails;
        }
    }
}