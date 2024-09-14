using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Npgsql;
using System.Windows.Controls.Primitives;

namespace GraphicsModule.Pages
{
    public partial class DailyUserPlanHoursAndMates : Page
    {
        public PlotModel PlotModel { get; set; }
        public List<WorkScheduleData> WorkSchedule { get; set; }

        public DailyUserPlanHoursAndMates()
        {
            InitializeComponent();
            LoadDailySchedule(); // Загружаем реальные данные
        }

        // Загрузка данных из базы данных
        private void LoadDailySchedule()
        {
            var connection = new Connection();
            var userId = Session.UserID; // Используем сохраненный id_user для текущего пользователя
            var today = DateTime.Now.Date; // Получаем текущую дату как тип DateTime

            // Логируем дату для проверки
            Console.WriteLine($"Today (DateTime): {today}");

            string query = @"
 SELECT
     p.id_project,
     c.date_start,
     c.date_end,
     u.name_user,
     w.start_time,
     w.end_time,
     cl.org_name
 FROM
     WorkSchedule w
 JOIN
     Project p ON w.id_project = p.id_project
 JOIN
     Users u ON w.id_user = u.id_user
 JOIN
     Contracts c ON p.id_contract = c.id_contract
 JOIN
     Organizations o ON c.id_org = o.id_org
 JOIN
     Clients cl ON o.id_client = cl.id_client
 WHERE
     w.id_project IN (
         SELECT id_project 
         FROM WorkSchedule 
         WHERE id_user = @userId AND work_date = @today::date
     ) 
     AND w.work_date = @today::date
     ORDER BY w.start_time"; // Используем дату в формате SQL

            using (var conn = connection.GetConnection())
            {
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@today", NpgsqlTypes.NpgsqlDbType.Date, today); // Передаем дату как тип Date

                    using (var reader = cmd.ExecuteReader())
                    {
                        WorkSchedule = new List<WorkScheduleData>();
                        while (reader.Read())
                        {
                            Console.WriteLine($"Reading row: Project ID: {reader.GetGuid(0)}, Start: {reader.GetTimeSpan(4)}, End: {reader.GetTimeSpan(5)}, User: {reader.GetString(3)}, Project Name: {reader.GetString(6)}");

                            var scheduleData = new WorkScheduleData
                            {
                                ProjectId = reader.GetGuid(0),
                                StartTime = reader.GetTimeSpan(4),
                                EndTime = reader.GetTimeSpan(5),
                                UserName = reader.GetString(3),
                                ProjectName = reader.GetString(6)
                            };

                            WorkSchedule.Add(scheduleData);
                        }

                        if (WorkSchedule.Count == 0)
                        {
                            Console.WriteLine("No data loaded from database.");
                        }
                        else
                        {
                            Console.WriteLine($"Total rows loaded: {WorkSchedule.Count}");
                        }

                        DataContext = this;
                        InitializePlotModel(WorkSchedule);
                    }
                }
            }
        }




        private void InitializePlotModel(List<WorkScheduleData> workSchedule)
        {
            // Создаем модель графика
            PlotModel = new PlotModel { Title = "Рабочие часы на день" };

            // Ось времени (часы) с ограничением от 0 до 24 часов
            var timeAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = 0,  // Начало дня (00:00)
                Maximum = 24, // Конец дня (24:00)
                Title = "Часы",
                MajorStep = 1,
                MinorStep = 1,
                IsPanEnabled = false, // Отключаем панорамирование
                IsZoomEnabled = false // Отключаем зумирование
            };
            PlotModel.Axes.Add(timeAxis);

            // Ось категорий (организации/проекты)
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Проекты",
                IsPanEnabled = false,
                IsZoomEnabled = false
            };

            // Добавляем названия проектов на ось категорий
            foreach (var schedule in workSchedule)
            {
                if (!categoryAxis.Labels.Contains(schedule.ProjectName))
                {
                    categoryAxis.Labels.Add(schedule.ProjectName);
                }
            }
            PlotModel.Axes.Add(categoryAxis);

            // Создаем серию для каждого проекта
            var barSeries = new RectangleBarSeries
            {
                FillColor = OxyColors.LightBlue,
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1
            };

            foreach (var schedule in workSchedule)
            {
                double start = schedule.StartTime.TotalHours;
                double end = schedule.EndTime.TotalHours;
                int index = categoryAxis.Labels.IndexOf(schedule.ProjectName);

                // Добавляем прямоугольники (бар) для графика
                barSeries.Items.Add(new RectangleBarItem(start, index - 0.4, end, index + 0.4));
            }

            // Добавляем серию данных в модель графика
            PlotModel.Series.Add(barSeries);

            // Присваиваем модель графика элементу PlotView
            DailySchedulePlot.Model = PlotModel;

            // Обновляем график
            PlotModel.InvalidatePlot(true);
        }

        // Модель данных для рабочего расписания
        public class WorkScheduleData
        {
            public Guid ProjectId { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string UserName { get; set; }
            public string ProjectName { get; set; }
        }

        private void Go_Back(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
