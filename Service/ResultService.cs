using Microsoft.Extensions.Logging;
using NTH.Common;
using NTH.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTH.Service
{
    public class ResultService
    {
        private readonly GoogleSheetsService _googleSheetsService;
        public ResultService(
        GoogleSheetsService googleSheetsService
        )
        {
            _googleSheetsService = googleSheetsService;
        }

        public async Task AddResult(ulong userId, ResultModel model)
        {
           await _googleSheetsService.AppendModelAsync(model, $"{userId}_{Constant.NAME_SHEET_RESULT}", Constant.HEADER_RESULT);
        }

        public async Task<bool> CheckCompletedTestToday(ulong userId)
        {
            var allResult = await _googleSheetsService.ReadValuesAsModelListAsync<ResultModel>($"{userId}_result");

            return allResult.Where(p => p.DoTestDate.HasValue 
                    && p.DoTestDate.Value == DateTime.Today 
                    && p.IsPassed.HasValue 
                    && p.IsPassed.Value)
                .Any();
        }
    }
}
