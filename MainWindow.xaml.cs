using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;

namespace CFFSPDeployUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string  m_spPath = "";
        private string  m_spListFile = "";
        private string  m_dbServer = "";
        private string  m_dbSchema = "";
        private SPMgr   m_spMgr;
        private Thread m_workerThread;

        public MainWindow()
        {
            InitializeComponent(); 
        }

        private void btnExecute_Click(object sender, RoutedEventArgs e)
        {
            bool result = validateInputs();
            if (result)
            {
                tbLog.Text = "";

                m_spMgr = new SPMgr(m_spPath, m_spListFile, m_dbServer, m_dbSchema);
                
                // Set data binding
                //Binding binding = new Binding();
                //binding.Source = m_spMgr;
                //binding.Path = new PropertyPath("LogInfo");
                //BindingOperations.SetBinding(tbLog, TextBox.TextProperty, binding);

                m_spMgr.LogInfoChanged += new EventHandler<LogInfoChangedArgs>(m_spMgr_LogInfoChanged);

                m_workerThread = new Thread(new ThreadStart(m_spMgr.doWork));

                m_workerThread.Start();
            }
        }

        private void m_spMgr_LogInfoChanged(object sender, LogInfoChangedArgs args)
        {
            tbLog.Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Normal,
            new Action(
                delegate() { 
                    tbLog.Text += args.LogInfo;
                    tbLog.Text += "\r\n";
                }
                )
            );
        }

        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = false;
            dialog.SelectedPath = m_spPath;
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                tbSPPath.Text = dialog.SelectedPath;
            }
        }

        private void btnSelectSPFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(m_spListFile);
            dialog.Filter = "Text documents (.txt)|*.txt";
            Nullable<bool> result = dialog.ShowDialog();
            if (result == true)
            {
                tbSPListFile.Text = dialog.FileName;
            }
        }

        private bool validateInputs()
        {
            m_spPath = tbSPPath.Text.Trim();
            m_spListFile = tbSPListFile.Text.Trim();
            m_dbServer = tbDBServer.Text.Trim();
            m_dbSchema = tbDBSchema.Text.Trim();

            if (!System.IO.Directory.Exists(m_spPath))
            {
                string msgText = string.Format("The specified SP Path {0} does not exist!", m_spPath);
                MessageBox.Show(msgText);
                return false;
            }

            if (!System.IO.File.Exists(m_spListFile))
            {
                string msgText = string.Format("The specified SP list file {0} does not exist!", m_spListFile);
                MessageBox.Show(msgText);
                return false;
            }

            if (string.IsNullOrEmpty(m_dbServer))
            {
                string msgText = "Please specify the DB server!";
                MessageBox.Show(msgText);
                return false;
            }

            if (string.IsNullOrEmpty(m_dbSchema))
            {
                string msgText = "Please specify DB Schema!";
                MessageBox.Show(msgText);
                return false;
            }

            return true;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // Read the config from last session
            SPMgr.readFromFile(out m_spPath, out m_spListFile, out m_dbServer, out m_dbSchema);
            tbSPPath.Text = m_spPath;
            tbSPListFile.Text = m_spListFile;
            tbDBServer.Text = m_dbServer;
            tbDBSchema.Text = m_dbSchema;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (m_spMgr != null)
            {
                m_spMgr.saveToFile();
            }
        }
    }
}
