using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Odbc;
using System.IO;
using System.Data;
using System.ComponentModel;

namespace CFFSPDeployUI
{
    class SPMgr 
    {
        //static private string SPLISTFILENAME = "CFF_SP_List.txt";
        static private string SPCONFIGFILENAME = "CFF_SP_Config.txt";
        static private string CONNSTRFORMAT = "DRIVER=HDBODBC32;UID=SYSTEM;PWD=manager;SERVERNODE={0};";
        static private string KEY_SPPATH = "SPPath";
        static private string KEY_SPLISTFILE = "SPListFile";
        static private string KEY_DBSERVER = "DBServer";
        static private string KEY_DBSCHEMA = "DBSchema";

        //private Dictionary<string, string> m_configMap = new Dictionary<string, string>();
        //private string m_strSPListFile = "";
        //private string m_strConfigFile = "";

        private string m_spPath = "";
        private string m_spListFile = "";
        private string m_dbServer = "";
        private string m_dbSchema = "";
        private string m_connString = "";
        private string m_createDBSP = "CFF_CREATEDBOBJECTS";

        private string m_logInfo = "";
        //public string LogInfo
        //{
        //    set
        //    {
        //        if (m_logInfo != value)
        //        {
        //            m_logInfo = value;
        //            OnPropertyChanged("LogInfo");
        //        }
        //    }
        //    get
        //    {
        //        return m_logInfo;
        //    }
        //}

        //public event PropertyChangedEventHandler PropertyChanged;
        //public void OnPropertyChanged(string propertyName)
        //{
        //    if (PropertyChanged != null)
        //    {
        //        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        //    }
        //}

        public event EventHandler<LogInfoChangedArgs> LogInfoChanged;
        private void OnLogInfoChanged(LogInfoChangedArgs args)
        {
            if (LogInfoChanged != null)
            {
                LogInfoChanged(this, args);
            }
        }

        private void FireLogInfoChangedEvent(string logInfo)
        {
            OnLogInfoChanged(new LogInfoChangedArgs(logInfo));
        }

        public SPMgr(string spPath, string spListFile, string dbServer, string dbSchema)
        {
            m_spPath = spPath;
            m_spListFile = spListFile;
            m_dbServer = dbServer;
            m_dbSchema = dbSchema;

            m_connString = string.Format(CONNSTRFORMAT, m_dbServer);
        }

        public static void readFromFile(out string spPath, out string spListFile, out string dbServer, out string dbSchema)
        {
            spPath = "";
            spListFile = "";
            dbServer = "";
            dbSchema = "";

            string configFile = Path.Combine(Path.GetTempPath(), SPCONFIGFILENAME);

            if (!File.Exists(configFile))
            {
                Console.WriteLine(string.Format("Config file {0} does not exist.", configFile));
                return;
            }

            // Parse config file
            Dictionary<string, string> configDict = new Dictionary<string, string>();
            using (StreamReader sr = new StreamReader(configFile))
            {
                string line;
                int lineNum = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    lineNum++;

                    // Skip the comment line
                    if (line.StartsWith("#"))
                        continue;

                    if (!string.IsNullOrEmpty(line))
                    {
                        string[] tokens = line.Split('=');
                        if (tokens.Length != 2)
                        {
                            Console.WriteLine(string.Format("Error in line {0}. The format should be KEY=VALUE.", lineNum));
                        }
                        else
                        {
                            configDict.Add(tokens[0].Trim(), tokens[1].Trim());
                        }
                    }
                }
            }

            try
            {
                spPath = configDict[KEY_SPPATH];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine(string.Format("{0} not find in config file.", KEY_SPPATH));
            }

            try
            {
                spListFile = configDict[KEY_SPLISTFILE];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine(string.Format("{0} not find in config file.", KEY_SPLISTFILE));
            }

            try
            {
                dbServer = configDict[KEY_DBSERVER];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine(string.Format("{0} not find in config file.", KEY_DBSERVER));
            }

            try
            {
                dbSchema = configDict[KEY_DBSCHEMA];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine(string.Format("{0} not find in config file.", KEY_DBSCHEMA));
            }
        }

        public void saveToFile()
        {
            string configFile = Path.Combine(Path.GetTempPath(), SPCONFIGFILENAME);
            using (StreamWriter sw = new StreamWriter(configFile))
            {
                string line = string.Format("{0}={1}", KEY_SPPATH, m_spPath);
                sw.WriteLine(line);

                line = string.Format("{0}={1}", KEY_SPLISTFILE, m_spListFile);
                sw.WriteLine(line);

                line = string.Format("{0}={1}", KEY_DBSERVER, m_dbServer);
                sw.WriteLine(line);

                line = string.Format("{0}={1}", KEY_DBSCHEMA, m_dbSchema);
                sw.WriteLine(line);
            }
        }

        public void doWork()
        {
            using (OdbcConnection conn = new OdbcConnection(m_connString))
            {
                try
                {
                    conn.Open();
                    string cmdStr = "SET SCHEMA " + m_dbSchema;
                    OdbcCommand cmd = new OdbcCommand(cmdStr, conn);
                    cmd.ExecuteNonQuery();

                    // get SP list
                    List<string> spList = new List<string>();
                    using (StreamReader sr = new StreamReader(m_spListFile))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (string.IsNullOrEmpty(line) != true)
                                spList.Add(line);
                        }
                    }

                    // get SP in DB
                    List<string> spInDBList = new List<string>();
                    cmdStr = string.Format("SELECT DISTINCT PROCEDURE_NAME FROM SYS.PROCEDURES WHERE SCHEMA_NAME = '{0}' AND PROCEDURE_NAME LIKE 'CFF%'", m_dbSchema);
                    //cmdStr = "SELECT distinct Procedure_Name from SYS.PROCEDURES where Procedure_Name like 'CFF%'";
                    cmd = new OdbcCommand(cmdStr, conn);
                    OdbcDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        string spName = dataReader[0].ToString();
                        spName = spName.Trim();
                        spInDBList.Add(spName);
                    }

                    // Drop SP
                    string tmpSPName = "";
                    foreach (string spName in spList)
                    {
                        tmpSPName = spName;
                        if (spInDBList.IndexOf(spName.ToUpper()) == -1)
                            continue;
                        try
                        {
                            cmdStr = "DROP PROCEDURE " + spName;
                            OdbcCommand dropCmd = new OdbcCommand(cmdStr, conn);
                            dropCmd.CommandType = CommandType.StoredProcedure;
                            dropCmd.ExecuteNonQuery();
                            //Console.WriteLine("Dropping " + tmpSPName);
                            //m_logInfo = m_logInfo + "Dropping " + tmpSPName + "\r\n";
                            FireLogInfoChangedEvent("Dropping " + tmpSPName);
                        }
                        catch (System.Exception)
                        {
                            //Console.WriteLine("Cannot drop " + tmpSPName);
                            //LogInfo = m_logInfo + "Cannot drop " + tmpSPName + "\r\n";
                            FireLogInfoChangedEvent("Cannot drop" + tmpSPName);
                        }
                    }

                    // Create SP
                    int count = 0;
                    foreach (string spName in spList)
                    {
                        tmpSPName = spName;
                        string spFile = Path.Combine(m_spPath, spName + ".txt");
                        string spText = "";
                        using (StreamReader sr = new StreamReader(spFile))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                spText += line;
                                spText += "\r\n";
                            }
                        }

                        //Console.WriteLine("Creating " + spName);
                        FireLogInfoChangedEvent("Creating " + spName);

                        OdbcCommand createSPCmd = new OdbcCommand(spText, conn);
                        createSPCmd.CommandType = CommandType.StoredProcedure;
                        createSPCmd.ExecuteNonQuery();

                        // Execute CFF_CreateDBObjects SP
                        if (spName == m_createDBSP)
                        {
                            cmdStr = "CALL " + spName + "()";
                            OdbcCommand spCmd = new OdbcCommand(cmdStr, conn);
                            spCmd.CommandType = CommandType.StoredProcedure;
                            spCmd.ExecuteNonQuery();
                            //Console.WriteLine("Executing " + spName);
                            //LogInfo = m_logInfo + "Executing " + spName + "\r\n";
                            FireLogInfoChangedEvent("Executing " + spName);
                        }

                        count++;
                    }

                    //Console.WriteLine("Done! SP created: " + count);
                    //LogInfo = m_logInfo + "Done! SP created: " + count + "\r\n";
                    FireLogInfoChangedEvent("Done! SP created: " + count);
                }
                catch (System.Exception ex)
                {
                    //Console.WriteLine(ex.ToString());
                    //LogInfo = m_logInfo + ex.ToString() + "\r\n";
                    FireLogInfoChangedEvent(ex.ToString());
                }
            }
        }
    }

    class LogInfoChangedArgs : EventArgs
    {
        public string LogInfo { get; private set; }
        public LogInfoChangedArgs(string logInfo)
        {
            LogInfo = logInfo;
        }
    }
}
