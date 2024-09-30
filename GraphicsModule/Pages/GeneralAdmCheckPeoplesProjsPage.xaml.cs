using Npgsql;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Utilities;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class GeneralAdmCheckPeoplesProjsPage : Page
    {
        public PlotModel PlotModel { get; set; }

        private List<ProjectData> projects;
        public List<string> Companies { get; set; }
        public List<string> Months { get; set; }
        public List<int> Years { get; set; }

        public GeneralAdmCheckPeoplesProjsPage()
        {
            InitializeComponent();
            LoadProjectsData();
            LoadComboBoxData();
        }

        private void LoadProjectsData()
        {
            var connection = new Connection();

            string query = @"
  SELECT 
      p.id_project,
      c.date_start,
      c.date_end,
      c.date1_start, 
      c.date2_end,
      s.status_name,
      cl.org_name,
      u.id_user,
      u.name_user,
      pu.phase1_start, 
      pu.phase1_end,
      pu.phase2_start, 
      pu.phase2_end
  FROM 
      ProjectUsers pu
  JOIN 
      Project p ON pu.id_project = p.id_project
  JOIN 
      Contracts c ON p.id_contract = c.id_contract
  JOIN 
      Status s ON p.id_status = s.id_status
  JOIN 
      Organizations o ON c.id_org = o.id_org
  JOIN 
      Clients cl ON o.id_client = cl.id_client
  JOIN 
      Users u ON pu.id_user = u.id_user
  WHERE 
      s.status_name != 'Архивирован'";

            using (var conn = connection.GetConnection())
            {
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        projects = new List<ProjectData>();
                        while (reader.Read())
                        {
                            var projectId = reader.GetGuid(0);
                            var projectData = projects.FirstOrDefault(p => p.ProjectId == projectId);

                            if (projectData == null)
                            {
                                projectData = new ProjectData
                                {
                                    ProjectId = projectId,
                                    StartDate = reader.GetDateTime(1),
                                    EndDate = reader.GetDateTime(2),
                                    ResumeDate = reader.GetDateTime(3),
                                    ResumeEndDate = reader.GetDateTime(4),
                                    Status = reader.GetString(5),
                                    OrganizationName = reader.GetString(6),
                                    TeamMembers = new List<TeamMemberData>()
                                };
                                projects.Add(projectData);
                            }

                            var teamMember = new TeamMemberData
                            {
                                UserId = reader.GetGuid(7),
                                UserName = reader.GetString(8),
                                Phase1Start = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                                Phase1End = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                Phase2Start = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11),
                                Phase2End = reader.IsDBNull(12) ? DateTime.MinValue : reader.GetDateTime(12)
                            };
                            Debug.WriteLine($"User: {teamMember.UserName}, Phase1: {teamMember.Phase1Start} - {teamMember.Phase1End}, Phase2: {teamMember.Phase2Start} - {teamMember.Phase2End}");
                            if (!projectData.TeamMembers.Any(u => u.UserId == teamMember.UserId))
                            {
                                projectData.TeamMembers.Add(teamMember);
                            }
                        }

                        InitializePlotModel();
                    }
                }
            }
        }

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = "Общий график работы с проектами" };

            var startDate = projects.Min(p => p.StartDate);
            var endDate = projects.Max(p => p.ResumeEndDate);

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(startDate),
                Maximum = DateTimeAxis.ToDouble(endDate),
                Title = "Время",
                StringFormat = "MMM yyyy",
                IntervalType = DateTimeIntervalType.Months,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            PlotModel.Axes.Add(timeAxis);

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Проекты",
                GapWidth = 0.5
            };

            foreach (var project in projects)
            {
                categoryAxis.Labels.Add(project.OrganizationName);
            }
            PlotModel.Axes.Add(categoryAxis);

            UpdatePlot(projects);
        }

        private void UpdatePlot(List<ProjectData> filteredProjects)
        {
            PlotModel.Annotations.Clear(); // Очищаем старые аннотации
            PlotModel.Series.Clear(); // Очищаем старые серии

            // Список доступных цветов
            var colors = new List<OxyColor>
            {
                OxyColors.LightGreen,
                OxyColors.Goldenrod,
                OxyColors.SandyBrown,
                OxyColors.Turquoise,
                OxyColors.Salmon,
                OxyColors.Coral,
                OxyColors.DarkKhaki
            };

            var timeAxis = (DateTimeAxis)PlotModel.Axes.FirstOrDefault(a => a is DateTimeAxis);
            double startDateDouble = timeAxis != null ? timeAxis.ActualMinimum : DateTimeAxis.ToDouble(projects.Min(p => p.StartDate));
            double endDateDouble = timeAxis != null ? timeAxis.ActualMaximum : DateTimeAxis.ToDouble(projects.Max(p => p.ResumeEndDate));

            foreach (var project in filteredProjects)
            {
                var projectIndex = filteredProjects.IndexOf(project);
                int participantCount = project.TeamMembers.Count;
                double height = 0.2 + Math.Max(0, (participantCount - 2) * 0.1);

                var barSeriesPhase1 = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName + " Phase 1"
                };

                if (project.StartDate != DateTime.MinValue && project.EndDate != DateTime.MinValue)
                {
                    var startDateDoublePhase1 = DateTimeAxis.ToDouble(project.StartDate);
                    var endDateDoublePhase1 = DateTimeAxis.ToDouble(project.EndDate);

                    barSeriesPhase1.Items.Add(new RectangleBarItem(startDateDoublePhase1, projectIndex - height, endDateDoublePhase1, projectIndex - 0.1));
                }

                PlotModel.Series.Add(barSeriesPhase1);

                var barSeriesPhase2 = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName + " Phase 2"
                };

                if (project.ResumeDate != DateTime.MinValue && project.ResumeEndDate != DateTime.MinValue)
                {
                    var resumeStartDouble = DateTimeAxis.ToDouble(project.ResumeDate);
                    var resumeEndDouble = DateTimeAxis.ToDouble(project.ResumeEndDate);

                    barSeriesPhase2.Items.Add(new RectangleBarItem(resumeStartDouble, projectIndex - height, resumeEndDouble, projectIndex - 0.1));
                }

                PlotModel.Series.Add(barSeriesPhase2);

                double verticalOffset = 0.05;
                int colorIndex = 0;

                foreach (var teamMember in project.TeamMembers)
                {
                    var teamMemberColor = colors[colorIndex % colors.Count];
                    teamMember.Color = teamMemberColor;
                    colorIndex++;

                    if (teamMember.Phase1Start != DateTime.MinValue && teamMember.Phase1End != DateTime.MinValue)
                    {
                        var phase1StartDouble = DateTimeAxis.ToDouble(teamMember.Phase1Start);
                        var phase1EndDouble = DateTimeAxis.ToDouble(teamMember.Phase1End);
                        double width = phase1EndDouble - phase1StartDouble;

                        // Полоска для Фазы 1
                        barSeriesPhase1.Items.Add(new RectangleBarItem(phase1StartDouble, projectIndex - height + verticalOffset, phase1EndDouble, projectIndex - height + verticalOffset + 0.05)
                        {
                            Color = teamMemberColor
                        });

                        PlotModel.Annotations.Add(new TextAnnotation
                        {
                            Text = teamMember.UserName,
                            TextPosition = new DataPoint((phase1StartDouble + phase1EndDouble) / 2, projectIndex - height + verticalOffset),
                            StrokeThickness = 0,
                            TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                            FontSize = CalculateDynamicFontSize(width),
                            TextColor = OxyColors.Black
                        });

                        verticalOffset += 0.05;
                    }

                    if (teamMember.Phase2Start != DateTime.MinValue && teamMember.Phase2End != DateTime.MinValue)
                    {
                        var phase2StartDouble = DateTimeAxis.ToDouble(teamMember.Phase2Start);
                        var phase2EndDouble = DateTimeAxis.ToDouble(teamMember.Phase2End);
                        double width = phase2EndDouble - phase2StartDouble;

                        // Полоска для Фазы 2
                        barSeriesPhase2.Items.Add(new RectangleBarItem(phase2StartDouble, projectIndex - height + verticalOffset, phase2EndDouble, projectIndex - height + verticalOffset + 0.05)
                        {
                            Color = teamMemberColor
                        });

                        PlotModel.Annotations.Add(new TextAnnotation
                        {
                            Text = teamMember.UserName,
                            TextPosition = new DataPoint((phase2StartDouble + phase2EndDouble) / 2, projectIndex - height + verticalOffset),
                            StrokeThickness = 0,
                            TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                            FontSize = CalculateDynamicFontSize(width),
                            TextColor = OxyColors.Black
                        });

                        verticalOffset += 0.05;
                    }
                }

                barSeriesPhase1.MouseDown += (s, e) =>
                {
                    UpdateCompanyName(project.OrganizationName);
                    UpdateTeamListBothPhases(project.TeamMembers);  // Обновляем участников обеих фаз
                    UpdateTeamListSelectedPhase(project.TeamMembers.Where(t => t.Phase1Start != DateTime.MinValue).ToList());  // Участники фазы 1
                };

                barSeriesPhase2.MouseDown += (s, e) =>
                {
                    UpdateCompanyName(project.OrganizationName);
                    UpdateTeamListBothPhases(project.TeamMembers);  // Обновляем участников обеих фаз
                    UpdateTeamListSelectedPhase(project.TeamMembers.Where(t => t.Phase2Start != DateTime.MinValue).ToList());  // Участники фазы 2
                };
            }

            ProjectTimeline.Model = PlotModel;
            ProjectTimeline.InvalidatePlot(true);
        }

        private void UpdateTeamListBothPhases(List<TeamMemberData> teamMembers)
        {
            TeamListBothPhases.ItemsSource = teamMembers;  // Участники всех фаз
        }

        private void UpdateTeamListSelectedPhase(List<TeamMemberData> teamMembers)
        {
            TeamListSelectedPhase.ItemsSource = teamMembers;  // Участники выбранной фазы
        }

        private void UpdateCompanyName(string organizationName)
        {
            CompanyName.Text = organizationName;
        }

        private double CalculateDynamicFontSize(double timeDifference)
        {
            double minSize = 10;
            double maxSize = 14;
            return Math.Max(minSize, Math.Min(maxSize, timeDifference / 15));
        }

        private void LoadComboBoxData()
        {
            Companies = projects.Select(p => p.OrganizationName).Distinct().ToList();
            Months = projects.Select(p => p.StartDate.ToString("MMMM")).Distinct().ToList();
            Years = projects.Select(p => p.StartDate.Year).Distinct().ToList();

            SortByCompany.ItemsSource = Companies;
            SortByMonth.ItemsSource = Months;
            SortByYear.ItemsSource = Years;
        }

        private void ApplyFilters()
        {
            var filteredProjects = projects.AsEnumerable();

            if (SortByCompany.SelectedItem is string selectedCompany)
            {
                filteredProjects = filteredProjects.Where(p => p.OrganizationName == selectedCompany);
            }

            if (SortByMonth.SelectedItem is string selectedMonth)
            {
                filteredProjects = filteredProjects.Where(p => p.StartDate.ToString("MMMM") == selectedMonth);
            }

            if (SortByYear.SelectedItem is int selectedYear)
            {
                filteredProjects = filteredProjects.Where(p => p.StartDate.Year == selectedYear);
            }

            UpdatePlot(filteredProjects.ToList());
        }

        private void SortByCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortByMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortByYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SortByCompany.SelectedItem = null;
            SortByMonth.SelectedItem = null;
            SortByYear.SelectedItem = null;
            UpdatePlot(projects);
        }

        private void Go_Back(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
