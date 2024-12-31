﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MES.Solution.Models
{
    public class WorkOrderModel : INotifyPropertyChanged
    {
        private int _sequence;
        private bool _isSelected;
        private string _status;

        // 시뮬레이션 관련
        private bool _isAutoMode;
        private string _simulationMode;

        public string WorkOrderNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int OrderQuantity { get; set; }
        public int ProductionQuantity { get; set; }
        public string Shift { get; set; }
        public string Remarks { get; set; }
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }
        public string ProductionLine { get; set; }

        public int Sequence
        {
            get => _sequence;
            set
            {
                if (_sequence != value)
                {
                    _sequence = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _startTime;
        private DateTime _completionTime;

        public DateTime StartTime
        {
            get { return _startTime; }
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CompletionTime
        {
            get { return _completionTime; }
            set
            {
                if (_completionTime != value)
                {
                    _completionTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAutoMode
        {
            get => _isAutoMode;
            set
            {
                if (_isAutoMode != value)
                {
                    _isAutoMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SimulationMode
        {
            get => _simulationMode;
            set
            {
                if (_simulationMode != value)
                {
                    _simulationMode = value;
                    IsAutoMode = value == "자동";
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}