using System;
using centrafuse.Plugins;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading;
using Microsoft.Win32;
using GeoTimeZone;

namespace TimeZone
{
    /// <summary>
    /// Timezone plugin for CentraFuse
    /// </summary>
    public class TimeZone : CFPlugin
    {
        #region Variables
        public const string PluginName = "TimeZone";
        private string settingsPath = CFTools.AppDataPath + "\\system\\settings.xml";
        private const string LogFile = PluginName + ".log";
        public static string LogFilePath = CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + LogFile;
        public static int intRefresh = 300;           // Default recheck interval (seconds)

        Thread threadTimeZone;                  // Worker thread
        private bool boolTimeZone = true;       // While true, thread runs

        static private bool LogEvents = true;   // LogEvents
        #endregion

        #region Construction

        /// <summary>
        /// Default constructor (creates the plugin and sets its properties).
        /// </summary>
        public TimeZone()
        {
            // Usually it is safe to just use the CF_initPlugin() override to do initialization
        }

        #endregion

        #region CFPlugin methods

        /// <summary>
        /// Initializes the plugin.  This is called from the main application
        /// when the plugin is first loaded.
        /// </summary>
        public override void CF_pluginInit()
        {
            try
            {
                //No GUI for this applications
                CF_params.isGUI = false;

                // CF3_initPlugin() Will configure pluginConfig and pluginLang automatically
                CF3_initPlugin(PluginName, CF_params.isGUI);

                //Clear old values from log file
                CFTools.writeModuleLog("startup", LogFilePath);

                //From http://wiki.centrafuse.com/wiki/Application-Description.ashx
                CF_params.settingsDisplayDesc = pluginLang.ReadField("/APPLANG/SETUP/DESCRIPTION");
                
                //Get settings
                LoadSettings();

                //Create and start the TimeZone thead
                threadTimeZone = new Thread(SubTimeZone);
                threadTimeZone.Start();
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.ToString()); }
        }

        /// <summary>
        /// This is called by the system when the plugin setup is clicked.
        /// </summary>
        /// <returns>Returns the dialog result.</returns>
        public override DialogResult CF_pluginShowSetup()
        {
            WriteLog("CF_pluginShowSetup");

            // Return DialogResult.OK for the main application to update from plugin changes.
            DialogResult returnvalue = DialogResult.Cancel;

            try
            {
                // Creates a new plugin setup instance
                Setup setup = new Setup(MainForm, pluginConfig, pluginLang);
                returnvalue = setup.ShowDialog();
                if (returnvalue == DialogResult.OK)
                {
                    LoadSettings();
                }
                setup.Close();
                setup = null;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.ToString()); }

            return returnvalue;
        }

        /// <summary>
        /// This is called by the system when it exits or the plugin has been deleted.
        /// </summary>
        public override void CF_pluginClose()
        {
            //Stop the TimeZone thread
            boolTimeZone = false;

            base.CF_pluginClose(); // calls form Dispose() method
        }

        #endregion

        private void LoadSettings()
        {
            // The display name is shown in the application to represent the plugin. This sets the display name from the configuration file
            CF_params.displayName = pluginLang.ReadField("/APPLANG/TIMEZONE/DISPLAYNAME");
            LogEvents = Boolean.Parse(pluginConfig.ReadField("/APPCONFIG/LOGEVENTS"));

            //Refresh interval
            try
            {
                intRefresh = Int32.Parse(pluginConfig.ReadField("/APPCONFIG/REFRESHINTERVAL"));
            }
            catch
            {
                intRefresh = 300;
                pluginConfig.WriteField("/APPCONFIG/REFRESHINTERVAL", intRefresh.ToString(), true);
            }
            WriteLog("Refresh interval: '" + intRefresh.ToString() + "'");
        }

        public static void WriteLog(string msg)
        {
            try
            {
                if (LogEvents)
                    CFTools.writeModuleLog(msg, LogFilePath);
            }
            catch { }
        }

        private void SubTimeZone()
        {
            WriteLog("Start of 'subTimeZone' thread");

            do
            {
                //Is refresh timer updated?
                WriteLog("Loop timer: " + intRefresh.ToString());

                //UpdateTimeZone.GPSUpdateTimeZone(50.961379, 1.855333);  // Europe/Paris
                //UpdateTimeZone.GPSUpdateTimeZone(50.437200, -3.555900); // Europe/London
                //UpdateTimeZone.GPSUpdateTimeZone(41.161054, -8.621660); // Europe/Porto

                try
                {
                    double latitude = 0.0;
                    double longitude = 0.0;

                    WriteLog("Raw CF Nav data: " + CF_navGetInfo(CFNavInfo.Latitude) + ", " + CF_navGetInfo(CFNavInfo.Longitude));
                    double.TryParse(CF_navGetInfo(CFNavInfo.Latitude), out latitude);
                    double.TryParse(CF_navGetInfo(CFNavInfo.Longitude), out longitude);

                    if (latitude == 0.0 && longitude == 0.0)
                    {
                        WriteLog("Latitude and longitude is 0 or invalid. Doing nothing");
                    }
                    else
                    {
                        WriteLog("We have GPS coordinates: lat:" + latitude.ToString() + ", Lon: " + longitude.ToString());
                        UpdateTimeZone.GPSUpdateTimeZone(latitude, longitude);
                    }
                }
                catch (Exception errmsg)
                {
                    WriteLog("Failed to process GPS source: " + errmsg.ToString());
                }

                Thread.Sleep(intRefresh * 1000); // Sleep before next loop
            }
            while (boolTimeZone);

            WriteLog("End of 'subTimezone' thread");
        }
        
        public void NotifyChange(string strMessage)
        {
            WriteLog("NotifyChange() - start()");
            CF_systemCommand(CF_Actions.SHOWINFO, "TZ: " + strMessage, "AUTOHIDE");
            WriteLog("NotifyChange() - end()");
        }
    }

    //Update the timezone based on GPS location
    public class UpdateTimeZone
    {
        //Get TimeZoneInformation
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int GetDynamicTimeZoneInformation(out DynamicTimeZoneInformation lptimezoneInformation);

        //Set TimeZoneInformation
        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int SetDynamicTimeZoneInformation([In] ref DynamicTimeZoneInformation lpTimeZoneInformation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DynamicTimeZoneInformation
        {
            [MarshalAs(UnmanagedType.I4)]
            public int bias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string standardName;
            public SystemTime StandardDate;
            [MarshalAs(UnmanagedType.I4)]
            public int standardbias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string daylightName;
            public SystemTime daylightDate;
            [MarshalAs(UnmanagedType.I4)]
            public int daylightBias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string timeZoneKeyName;
            [MarshalAs(UnmanagedType.U1)]
            public bool dynamicDaylightTimedisabled;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SystemTime
        {
            [MarshalAs(UnmanagedType.U2)]
            public ushort year;
            [MarshalAs(UnmanagedType.U2)]
            public ushort month;
            [MarshalAs(UnmanagedType.U2)]
            public ushort dayOfWeek;
            [MarshalAs(UnmanagedType.U2)]
            public ushort day;
            [MarshalAs(UnmanagedType.U2)]
            public ushort hour;
            [MarshalAs(UnmanagedType.U2)]
            public ushort minute;
            [MarshalAs(UnmanagedType.U2)]
            public ushort second;
            [MarshalAs(UnmanagedType.U2)]
            public ushort milliseconds;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct REG_TZI_FORMAT
        {
            [MarshalAs(UnmanagedType.I4)]
            public int bias;
            [MarshalAs(UnmanagedType.I4)]
            public int standardbias;
            [MarshalAs(UnmanagedType.I4)]
            public int daylightBias;
            public SystemTime standardDate;
            public SystemTime daylightDate;
        }

        private const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";

        public static bool GPSUpdateTimeZone(double lat, double lng)
        {
            try
            {
                //Clear any cache before reading the TZ to ensure we're reading current information
                CultureInfo.CurrentCulture.ClearCachedData();
                TimeZoneInfo.ClearCachedData();
                System.TimeZone currentTimeZone = System.TimeZone.CurrentTimeZone;
                //Get IANA name of active timezone
                string tz = TimeZoneLookup.GetTimeZone(lat, lng).Result;
                //Translate active timezone to Windows format
                string tzidTimezoneName = translateFromWindowsTimezone(tz);
                TimeZone.WriteLog("Current timezone: '" + currentTimeZone.StandardName + "' / GPS timezone: '" + tzidTimezoneName + "'");
                if (currentTimeZone.StandardName.Equals(tzidTimezoneName) == false)
                {
                    TimeZone.WriteLog("Changed timezone. New timezone to use: '" + tzidTimezoneName + "'");
                    //Setting timezone is filtered from token by default. Add it
                    if (TokenPrivilegesAccess.EnablePrivilege(SE_TIME_ZONE_NAME) != true)
                    {
                        TimeZone.WriteLog("Failed to get token to change timezone information");
                        CheckWin32Error();
                        return false;
                    }
                    //Get some of the TZ settings to use from Windows
                    var newTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tzidTimezoneName);
                    //Get the rest of TZ settings to use from Registry
                    REG_TZI_FORMAT RTZI = GetRegTziFormat(tzidTimezoneName);
                    //Set the new TZ
                    DynamicTimeZoneInformation tzi = new DynamicTimeZoneInformation();
                    tzi.dynamicDaylightTimedisabled = false;
                    tzi.standardName = newTimeZoneInfo.StandardName;
                    tzi.daylightName = newTimeZoneInfo.DaylightName;
                    tzi.bias = RTZI.bias;
                    tzi.daylightBias = RTZI.daylightBias;
                    tzi.daylightDate = RTZI.standardDate;
                    tzi.standardbias = RTZI.standardbias;
                    tzi.StandardDate = RTZI.standardDate;
                    tzi.timeZoneKeyName = tzidTimezoneName;
                    if (SetDynamicTimeZoneInformation(ref tzi) == 0)
                    {
                        TimeZone.WriteLog("Failed to update the timeZone information");
                        CheckWin32Error();
                    }
                    else
                    {
                        //Let the world know the timezone changed
                        CultureInfo.CurrentCulture.ClearCachedData();
                        TimeZoneInfo.ClearCachedData();
                        SendMessage.BroadcastSettingsChange();
                        //Get the new TZ
                        DynamicTimeZoneInformation newTZI = new DynamicTimeZoneInformation();
                        GetDynamicTimeZoneInformation(out newTZI);
                        TimeZone.WriteLog("Timezone changed to '" + newTZI.standardName + "'");

                        TimeZone tzTmp = new TimeZone();
                        tzTmp.NotifyChange(newTZI.standardName);
                    }
                    //Setting timezone is filtered from token by default. Remove it
                    if (TokenPrivilegesAccess.DisablePrivilege(SE_TIME_ZONE_NAME) != true)
                    {
                        TimeZone.WriteLog("Failed to remove token to chagne timezone information");
                        CheckWin32Error();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to update TimeZone: " + ex.ToString());
            }
            return true;
        }

        //Note: Dictionary is not complete, several entries missing. Feel free to contribute...
        //https://stackoverflow.com/questions/25212733/translate-time-zone-names-to-time-ids-in-xcode
        //http://www.unicode.org/cldr/charts/29/supplemental/zone_tzid.html
        private static string translateFromWindowsTimezone(string timezoneName)
        {
            try
            {
                Dictionary<string, string> timezoneDictionary = new Dictionary<string, string>
                {
                    { "Africa/Casablanca", "Morocco Standard Time" },
                    { "Africa/Windhoek", "Namibia Standard Time" },
                    { "Africa/Lagos", "W. Central Africa Standard Time" },
                    { "Africa/Johannesburg", "South Africa Standard Time"},
                    { "Africa/Nairobi", "E. Africa Standard Time" },
                    { "Africa/Cairo", "Egypt Standard Time" },

                    { "America/Regina", "Canada Central Standard Time" },
                    { "America/Buenos_Aires", "Argentina Standard Time" },
                    { "America/Anchorage", "Alaskan Standard Time" },
                    { "America/Halifax", "Atlantic Standard Time" },
                    { "America/Bahia", "Bahia Standard Time" },
                    { "America/Guatemala", "Central America Standard Time" },
                    { "America/Cuiaba", "Central Brazilian Standard Time" },
                    { "America/Chicago", "Central Standard Time" },
                    { "America/Mexico_City", "Central Standard Time (Mexico)" },
                    { "America/Godthab", "Greenland Standard Time" },
                    { "America/Sao_Paulo", "E. South America Standard Time" },
                    { "America/New_York", "Eastern Standard Time" },
                    { "America/Denver", "Mountain Standard Time" },
                    { "America/Chihuahua", "Mountain Standard Time (Mexico)" },
                    { "America/Montevideo", "Montevideo Standard Time" },
                    { "America/St_Johns", "Newfoundland Standard Time" },
                    { "America/Santiago", "Pacific SA Standard Time" },
                    { "America/Los_Angeles", "Pacific Standard Time" },
                    { "America/Santa_Isabel", "Pacific Standard Time (Mexico)"},
                    { "America/Asuncion", "Paraguay Standard Time"},
                    { "America/Cayenne", "SA Eastern Standard Time"},
                    { "America/Bogota", "SA Pacific Standard Time"},
                    { "America/La_Paz", "SA Western Standard Time"},
                    { "America/Caracas", "Venezuela Standard Time" },
                    { "America/Indianapolis", "US Eastern Standard Time"},
                    { "America/Phoenix", "US Mountain Standard Time"},

                    { "Arctic/Lonyearbyen", "W. Europe Standard Time" },

                    { "Asia/Kabul", "Afghanistan Standard Time"},
                    { "Asia/Riyadh", "Arab Standard Time" },
                    { "Asia/Baghdad", "Arabic Standard Time" },
                    { "Asia/Baku", "Azerbaijan Standard Time" },
                    { "Asia/Shanghai", "China Standard Time" },
                    { "Asia/Dhaka", "Bangladesh Standard Time" },
                    { "Asia/Yerevan", "Caucasus Standard Time" },
                    { "Asia/Almaty", "Central Asia Standard Time" },
                    { "Asia/Nicosia", "E. Europe Standard Time" },
                    { "Asia/Yekaterinburg", "Ekaterinburg Standard Time" },
                    { "Asia/Tbilisi", "Georgian Standard Time" },
                    { "Asia/Calcutta", "India Standard Time" },
                    { "Asia/Tehran", "Iran Standard Time" },
                    { "Asia/Jerusalem", "Israel Standard Time" },
                    { "Asia/Amman", "Jordan Standard Time" },
                    { "Asia/Seoul", "Korea Standard Time" },
                    { "Asia/Beirut", "Middle East Standard Time" },
                    { "Asia/Rangoon", "Myanmar Standard Time" },
                    { "Asia/Novosibirsk", "N. Central Asia Standard Time" },
                    { "Asia/Katmandu", "Nepal Standard Time" },
                    { "Asia/Irkutsk", "North Asia East Standard Time" },
                    { "Asia/Krasnoyarsk", "North Asia Standard Time" },
                    { "Asia/Karachi", "Pakistan Standard Time"},
                    { "Asia/Bangkok", "SE Asia Standard Time"},
                    { "Asia/Singapore", "Singapore Standard Time"},
                    { "Asia/Colombo", "Sri Lanka Standard Time"},
                    { "Asia/Damascus", "Syria Standard Time"},
                    { "Asia/Taipei", "Taipei Standard Time"},
                    { "Asia/Tokyo", "Tokyo Standard Time"},
                    { "Asia/Ulaanbaatar", "Ulaanbaatar Standard Time" },
                    { "Asia/Vladivostok", "Vladivostok Standard Time" },
                    { "Asia/Tashkent", "West Asia Standard Time" },
                    { "Asia/Yakutsk", "Yakutsk Standard Time" },

                    { "Atlantic/Azores", "Azores Standard Time" },
                    { "Atlantic/Cape_Verde", "Cape Verde Standard Time" },
                    { "Atlantic/Reykjavik", "Greenwich Standard Time" },

                    { "Australia/Darwin", "AUS Central Standard Time" },
                    { "Australia/Adelaide", "Cen. Australia Standard Time" },
                    { "Australia/Broken_Hill", "Cen. Australia Standard Time" },
                    { "Australia/Brisbane", "E. Australia Standard Time" },
                    { "Australia/Sydney", "E. Australia Standard Time" },
                    { "Australia/Melbourne", "E. Australia Standard Time" },
                    { "Australia/Lindeman", "E. Australia Standard Time" },
                    { "Australia/Perth", "W. Australia Standard Time" },
                    { "Australia/Hobart", "Tasmania Standard Time"},
                    { "Australia/Currie", "Tasmania Standard Time"},

                    { "Europe/Budapest", "Central Europe Standard Time" },
                    { "Europe/Warsaw", "Central European Standard Time" },
                    { "Europe/Tirane", "Central European Standard Time" },
                    { "Europe/Prague", "Central European Standard Time" },
                    { "Europe/Podgorica", "Central European Standard Time" },
                    { "Europe/Belgrave", "Central European Standard Time" },
                    { "Europe/Ljubljana", "Central European Standard Time" },
                    { "Europe/Bratislava", "Central European Standard Time" },
                    { "Europe/Sarajevo", "Central European Standard Time" },
                    { "Europe/Zagreb", "Central European Standard Time" },
                    { "Europe/Skopje", "Central European Standard Time" },                    
                    { "Europe/Kiev", "FLE Standard Time" },
                    { "Europe/Mariehamn", "FLE Standard Time" },
                    { "Europe/Sofia", "FLE Standard Time" },
                    { "Europe/Tallinn", "FLE Standard Time" },
                    { "Europe/Helsinki", "FLE Standard Time" },
                    { "Europe/Vilnius", "FLE Standard Time" },
                    { "Europe/Riga", "FLE Standard Time" },
                    { "Europe/Uzhgorod", "FLE Standard Time" },
                    { "Europe/Zaporozhye", "FLE Standard Time" },
                    { "Europe/London", "GMT Standard Time" },
                    { "Europe/Lisbon", "GMT Standard Time" },
                    { "Europe/Guernsey", "GMT Standard Time" },
                    { "Europe/Duplin", "GMT Standard Time" },
                    { "Europe/Isle_of_Man", "GMT Standard Time" },
                    { "Europe/Jersey", "GMT Standard Time" },
                    { "Europe/Madeira", "GMT Standard Time" },
                    { "Europe/Athens", "GTB Standard Time" },
                    { "Europe/Bucharest", "GTB Standard Time" },
                    { "Europe/Kaliningrad", "Kaliningrad Standard Time" },
                    { "Europe/Paris", "Romance Standard Time"},
                    { "Europe/Brussels", "Romance Standard Time"},
                    { "Europe/Copenhagen", "Romance Standard Time"},
                    { "Europe/Madrid", "Romance Standard Time"},
                    { "Europe/Moscow", "Russian Standard Time"},
                    { "Europe/Simferopol", "Russian Standard Time"},
                    { "Europe/Volgograd", "Russian Standard Time"},
                    { "Europe/Istanbul", "Turkey Standard Time"},
                    { "Europe/Berlin", "W. Europe Standard Time" },
                    { "Europe/Andorra", "W. Europe Standard Time" },
                    { "Europe/Vienna", "W. Europe Standard Time" },
                    { "Europe/Zurich", "W. Europe Standard Time" },
                    { "Europe/Busingen", "W. Europe Standard Time" },
                    { "Europe/Gibraltar", "W. Europe Standard Time" },
                    { "Europe/Rome", "W. Europe Standard Time" },
                    { "Europe/Vaduz", "W. Europe Standard Time" },
                    { "Europe/Luxembourg", "W. Europe Standard Time" },
                    { "Europe/Monaco", "W. Europe Standard Time" },
                    { "Europe/Malta", "W. Europe Standard Time" },
                    { "Europe/Amsterdam", "W. Europe Standard Time" },
                    { "Europe/Oslo", "W. Europe Standard Time" },
                    { "Europe/Stockholm", "W. Europe Standard Time" },
                    { "Europe/San_Marino", "W. Europe Standard Time" },
                    { "Europe/Vatican", "W. Europe Standard Time" },

                    { "Indian/Mauritius", "Mauritius Standard Time" },

                    { "Pacific/Guadalcanal", "Central Pacific Standard Time" },
                    { "Pacific/Fiji", "Fiji Standard Time" },
                    { "Pacific/Honolulu", "Hawaiian Standard Time" },
                    { "Pacific/Auckland", "New Zealand Standard Time" },
                    { "Pacific/Apia", "Samoa Standard Time"},
                    { "Pacific/Tongatapu", "Tonga Standard Time"},
                    { "Pacific/Port_Moresby", "West Pacific Standard Time" },
                    
                    { "Etc/GMT-12", "UTC+12" },
                    { "Etc/GMT", "UTC" },
                    { "Etc/GMT+2", "UTC-02" },
                    { "Etc/GMT+11", "UTC-11" },
                    { "Etc/GMT+12", "Dateline Standard Time" }
                };
                return timezoneDictionary[timezoneName];
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to translate IANA name '" + timezoneName + "' to Windows name: " + ex.ToString());
                return null;
            }
        }
        
        //Get the TZ information from registry
        private static REG_TZI_FORMAT GetRegTziFormat(string TimeZoneName)
        {
            REG_TZI_FORMAT rtzi = new REG_TZI_FORMAT();
            try
            {
                
                var timeZoneRegistryPath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones");
                var subKey = timeZoneRegistryPath.GetSubKeyNames().Where(s => s == TimeZoneName).First();
                byte[] tziRegValue = (byte[])timeZoneRegistryPath.OpenSubKey(subKey).GetValue("TZI");
                if (tziRegValue == null || tziRegValue.Length != 44)
                {
                    throw new ArgumentException("Invalid REG_TZI_FORMAT");
                }
                
                int iSize = tziRegValue.Length;
                IntPtr buffer = Marshal.AllocHGlobal(iSize);
                Marshal.Copy(tziRegValue, 0, buffer, iSize);
                rtzi = (REG_TZI_FORMAT)Marshal.PtrToStructure(buffer, typeof(REG_TZI_FORMAT));
                Marshal.FreeHGlobal(buffer);
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to get TZI information from Registry: " + ex.ToString());
            }
            return rtzi;
        }

        //Success/failure
        private static void CheckWin32Error()
        {
            try
            {
                int rc = Marshal.GetLastWin32Error();
                if (rc != 0)
                    throw new System.ComponentModel.Win32Exception(rc);
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to GetLastWin32Error(): " + ex.ToString());
            }
        }
    }

    //Get/remove token to adjust timezone
    public class TokenPrivilegesAccess
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int OpenProcessToken(int ProcessHandle, int DesiredAccess, ref int tokenhandle);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int LookupPrivilegeValue(string lpsystemname, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        private static extern int AdjustTokenPrivileges(int TokenHandle, int DisableAllPrivileges, [MarshalAs(UnmanagedType.Struct)]ref TOKEN_PRIVILEGE NewState, int bufferlength, int PreviousState, int Returnlengh);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern int GetCurrentProcess();
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LUID
        {
            internal uint LowPart;
            internal uint HighPart;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LUID_AND_ATTRIBUTES
        {
            internal LUID Luid;
            internal uint Attributes;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TOKEN_PRIVILEGE
        {
            internal uint PrivilegeCount;
            internal LUID_AND_ATTRIBUTES Privilege;
        }
        private const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        private const int TOKEN_QUERY = 0x00000008;
        private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        public static bool EnablePrivilege(string privilege)
        {
            try
            {
                int token = 0;
                int retVal = 0;
                TOKEN_PRIVILEGE TP = new TOKEN_PRIVILEGE();
                LUID LD = new LUID();
                retVal = OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref token);
                retVal = LookupPrivilegeValue(null, privilege, ref LD);
                TP.PrivilegeCount = 1;
                var luidAndAtt = new LUID_AND_ATTRIBUTES();
                luidAndAtt.Attributes = SE_PRIVILEGE_ENABLED;
                luidAndAtt.Luid = LD;
                TP.Privilege = luidAndAtt;
                retVal = AdjustTokenPrivileges(token, 0, ref TP, 1024, 0, 0);
                return true;
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to get token, " + ex.ToString());
                return false;
            }
        }
        public static bool DisablePrivilege(string privilege)
        {
            try
            {
                int token = 0;
                int retVal = 0;
                TOKEN_PRIVILEGE TP = new TOKEN_PRIVILEGE();
                LUID LD = new LUID();
                retVal = OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref token);
                retVal = LookupPrivilegeValue(null, privilege, ref LD);
                TP.PrivilegeCount = 1;
                var luidAndAtt = new LUID_AND_ATTRIBUTES();
                // Attributes should be none (not set) to disable privilege
                luidAndAtt.Luid = LD;
                TP.Privilege = luidAndAtt;
                retVal = AdjustTokenPrivileges(token, 0, ref TP, 1024, 0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    //Send Windows messages
    public class SendMessage
    {
        //Send broadcast message
        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int SendMessageTimeout(int hwnd, int msg, int wParam, string lParam, int fuflags, int utimeout, ref int lpdwResult);
        private const int WM_SETTINGSCHANGE = 0X1A;
        private const int HWND_BROADCAST = (-1);
        private const int SMTO_ABORTIFHUNG = 0x02;
        public static int BroadcastSettingsChange()
        {
            int result = 0;
            try
            {
                int rtnValue = 0;
                result = SendMessageTimeout(HWND_BROADCAST, WM_SETTINGSCHANGE, 0, "intl", SMTO_ABORTIFHUNG, 5000, ref rtnValue);
            }
            catch (Exception ex)
            {
                TimeZone.WriteLog("Failed to send WinowsMessage(): " + ex.ToString());
            }
            return result;
        }
    }
}
