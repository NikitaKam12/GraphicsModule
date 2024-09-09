using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace GraphicsModule.Pages
{
    public partial class CreateAdminProjPage : Page
    {
        private readonly Connection _connection;

        public CreateAdminProjPage()
        {
            InitializeComponent();
            _connection = new Connection();
            LoadStatusComboBox();
        }

        private void Go_Back(object Sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void LoadStatusComboBox()
        {
            try
            {
                string query = "SELECT id_status, status_name FROM Status";
                using (var conn = _connection.GetConnection())
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                StatusComboBox.Items.Add(new ComboBoxItem
                                {
                                    Content = reader["status_name"].ToString(),
                                    Tag = reader["id_status"]
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статусов: {ex.Message}");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string orgName = Org_Name.Text;
                long ogrn = long.Parse(OGRN.Text); // Преобразование текста в long
                long inn = long.Parse(INN.Text);   // Преобразование текста в long
                string okvd = OKVD.Text;
                string descrOkvd2 = Descr_okvd_2.Text;
                string chapterOkvd2 = Chapter_okvd_2.Text;
                string descrOkvd = Descr_okvd.Text;
                int amountProfitLastYear = int.Parse(Amount_Profit_Last_Year.Text);
                int amountProfitReportYear = int.Parse(Amount_Profit_Report_Year.Text);
                int sumAssetsLastYear = int.Parse(Sum_Assets_Last_Year.Text);
                int sumAssetsReportYear = int.Parse(Sum_Assets_Report_Year.Text);
                DateTime dateStart = DateTime.Parse(Date_Start.Text);
                DateTime dateEnd = DateTime.Parse(Date_End.Text);
                var selectedStatus = (ComboBoxItem)StatusComboBox.SelectedItem;
                Guid statusId = (Guid)selectedStatus.Tag;

                // Вставляем данные в таблицу Clients
                string insertClientQuery = "INSERT INTO Clients (client_name, org_name) VALUES (@orgName, @orgName) RETURNING id_client";
                Guid clientId;
                using (var conn = _connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(insertClientQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@orgName", orgName);
                        clientId = (Guid)cmd.ExecuteScalar();
                    }
                }

                // Вставляем данные в таблицу Assets_Criteria_Client
                string insertAssetsCriteriaQuery = "INSERT INTO Assets_Criteria_Client (amount_profit_last_year, amount_profit_report_year, sum_assets_last_year, sum_assets_report_year) VALUES (@amountProfitLastYear, @amountProfitReportYear, @sumAssetsLastYear, @sumAssetsReportYear) RETURNING id_assets_and_criteria";
                Guid assetsCriteriaId;
                using (var conn = _connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(insertAssetsCriteriaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@amountProfitLastYear", amountProfitLastYear);
                        cmd.Parameters.AddWithValue("@amountProfitReportYear", amountProfitReportYear);
                        cmd.Parameters.AddWithValue("@sumAssetsLastYear", sumAssetsLastYear);
                        cmd.Parameters.AddWithValue("@sumAssetsReportYear", sumAssetsReportYear);
                        assetsCriteriaId = (Guid)cmd.ExecuteScalar();
                    }
                }

                // Вставляем данные в таблицу Organizations (без передачи org_name)
                string insertOrgQuery = "INSERT INTO Organizations (OGRN, INN, OKVD, description_OKVD_form2, chapter_OKVD_form2, description_OKVD, id_client, id_assets_and_criteria) VALUES (@OGRN, @INN, @OKVD, @descrOkvd2, @chapterOkvd2, @descrOkvd, @clientId, @assetsCriteriaId) RETURNING id_org";
                Guid orgId;
                using (var conn = _connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(insertOrgQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@OGRN", ogrn);  // Передаем long в OGRN
                        cmd.Parameters.AddWithValue("@INN", inn);    // Передаем long в INN
                        cmd.Parameters.AddWithValue("@OKVD", okvd);
                        cmd.Parameters.AddWithValue("@descrOkvd2", descrOkvd2);
                        cmd.Parameters.AddWithValue("@chapterOkvd2", chapterOkvd2);
                        cmd.Parameters.AddWithValue("@descrOkvd", descrOkvd);
                        cmd.Parameters.AddWithValue("@clientId", clientId);
                        cmd.Parameters.AddWithValue("@assetsCriteriaId", assetsCriteriaId);
                        orgId = (Guid)cmd.ExecuteScalar();
                    }
                }

                // Вставляем данные в таблицу Contracts
                string insertContractQuery = "INSERT INTO Contracts (date_start, date_end, id_org) VALUES (@dateStart, @dateEnd, @orgId) RETURNING id_contract";
                Guid contractId;
                using (var conn = _connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(insertContractQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@dateStart", dateStart);
                        cmd.Parameters.AddWithValue("@dateEnd", dateEnd);
                        cmd.Parameters.AddWithValue("@orgId", orgId);
                        contractId = (Guid)cmd.ExecuteScalar();
                    }
                }

                // Вставляем данные в таблицу Project
                string insertProjectQuery = "INSERT INTO Project (id_contract, id_status) VALUES (@contractId, @statusId)";
                using (var conn = _connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(insertProjectQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@contractId", contractId);
                        cmd.Parameters.AddWithValue("@statusId", statusId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Проект успешно создан!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании проекта: {ex.Message}");
            }
        }

    }
}
