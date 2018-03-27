﻿using AutoMapper;
using CoralTime.BL.Interfaces.Reports;
using CoralTime.Common.Constants;
using CoralTime.DAL.Repositories;
using CoralTime.ViewModels.Reports;
using CoralTime.ViewModels.Reports.Request.Grid;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace CoralTime.BL.Services.Reports.Export
{
    public partial class ReportsExportService : BaseService, IReportExportService
    {
        private IHostingEnvironment _environment { get; }
        private readonly IConfiguration _configuration;
        private readonly IReportsService _reportService;

        public ReportsExportService(UnitOfWork uow, IMapper mapper, IConfiguration configuration, IHostingEnvironment environment, IReportsService reportService)
            : base(uow, mapper)
        {
            _configuration = configuration;
            _environment = environment;
            _reportService = reportService;
        }

        #region Constants.

        private const string ExtensionXLSX = ".xlsx";
        private const string ExtensionCSV = ".csv";
        private const string ExtensionPDF = ".pdf";

        private const string ContentTypeXLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        //private const string ContentTypeXLSX = "application/octet-stream";
        private const string ContentTypeCSV = "application/csv";
        private const string ContentTypePDF = "application/pdf";

        private enum FileType
        {
            Excel = 0,
            CSV = 1,
            PDF = 2
        }

        #endregion

        #region Properties. 

        private string FileName { get; set; } = Constants.CoralTime;

        private string ContentType { get; set; } = string.Empty;

        private DateTime DateFrom { get; set; }

        private DateTime DateTo { get; set; }

        #endregion

        #region Export Excel, CSV, PDF. 

        public async Task<FileResult> ExportFileGroupedByTypeAsync(ReportsGridView reportsGridData, HttpContext httpContext)
        {
            var groupByType = _reportService.GetReportsGroupingBy(reportsGridData);

            var fileOfBytes = await CreateReportsFileOfBytesAsync(reportsGridData, groupByType);
            var fileStreamResult = SaveFileToFileStreamResult(httpContext, fileOfBytes);

            return fileStreamResult;
        }

        #endregion

        #region Export Excel, CSV, PDF. (Common methods)

        private async Task<byte[]> CreateReportsFileOfBytesAsync(ReportsGridView reportsGridView, ReportTotalView reportTotalView)
        {
            DateFrom = _reportService.DateFrom;
            DateTo = _reportService.DateTo;

            var fileOfBytes = new byte[0];

            UpdateFileName();

            switch (reportsGridView.FileTypeId ?? 0)
            {
                case (int) FileType.Excel:
                {
                    FileName = FileName + ExtensionXLSX;
                    //fileOfBytes = CreateFileExcel(reportTotalView);
                    ContentType = ContentTypeXLSX;

                    break;
                }

                case (int) FileType.CSV:
                {
                    FileName = FileName + ExtensionCSV;
                    //file = CreateFileCSV(reportTotalView);
                    ContentType = ContentTypeCSV;

                    break;
                }

                case (int) FileType.PDF:
                {
                    FileName = FileName + ExtensionPDF;
                    fileOfBytes = await CreateFilePDFAsync(reportTotalView);
                    ContentType = ContentTypePDF;

                    break;
                }
            }

            return fileOfBytes;
        }

        private FileStreamResult SaveFileToFileStreamResult(HttpContext httpContext, byte[] fileByte)
        {
            httpContext.Response.ContentType = ContentType;

            var fileStreamResult = new FileStreamResult(new MemoryStream(fileByte), new MediaTypeHeaderValue(ContentType))
            {
                FileDownloadName = FileName
            };

            return fileStreamResult;
        }
        
        private void UpdateFileName()
        {
            if (!string.IsNullOrEmpty(_reportService.SingleFilteredProjectName))
            {
                FileName = FileName + " " + _reportService.SingleFilteredProjectName + " " + GetAbbreviatedMonthName(DateFrom) + " - " + GetAbbreviatedMonthName(DateTo);
            }
            else
            {
                FileName = FileName + " Reports " + GetAbbreviatedMonthName(DateFrom) + " - " + GetAbbreviatedMonthName(DateTo);
            }
        }

        private string GetAbbreviatedMonthName(DateTime date)
        {
            return CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month) + " " + date.Day;
        }

        #endregion
    }
}