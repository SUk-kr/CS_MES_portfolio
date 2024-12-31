using LiveCharts;
using System;
using System.Collections.Generic;

namespace MES.Solution.Models.Equipment
{
    // 설비 카드 모델
    public class EquipmentCardModel
    {
        public string EquipmentCode { get; set; }
        public string ProductionLine { get; set; }
        public decimal Temperature { get; set; }
        public decimal Humidity { get; set; }
        public string Status { get; set; }
        public SeriesCollection ChartData { get; set; }
        public List<DateTime> Dates { get; set; }
        public DateTime InspectionDate { get; set; }


    }

    // 설비 점검 일정 모델
   

    // 측정 데이터 모델
    public class MeasurementData
    {
        public string EquipmentCode { get; set; }
        public string ProductionLine { get; set; }
        public decimal Temperature { get; set; }
        public decimal Humidity { get; set; }
        public DateTime InspectionDate { get; set; }
        public string Status { get; set; }
    }
}
