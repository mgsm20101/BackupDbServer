using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BackupDbServer.Views
{
    /// <summary>
    /// Interaction logic for DatabaseDetailsWindow.xaml
    /// </summary>
    public partial class DatabaseDetailsWindow : Window
    {
        public DatabaseDetailsWindow(string dbName, List<BackupEntry> allBackups)
        {
            InitializeComponent();

            HeaderText.Text = $"سجل النسخ الاحتياطي لـ: {dbName}";

            var last20 = allBackups
                .Where(b => b.Database == dbName)
                .OrderByDescending(b => b.BackupTime)
                .Take(20)
                .ToList();

            DetailsGrid.ItemsSource = last20;
        }

        public DatabaseDetailsWindow()
        {
            InitializeComponent();
        }
    }
}
