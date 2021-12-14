﻿global using static BitFab.KW1281Test.Program;

using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace BitFab.KW1281Test
{
    class Program
    {
        public static ILog Log { get; private set; } = new ConsoleLog();

        static void Main(string[] args)
        {
            try
            {
                Log = new FileLog("KW1281Test.log");

                var tester = new Program();
                tester.Run(args);
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Caught: {ex.GetType()} {ex.Message}");
                Log.WriteLine($"Unhandled exception: {ex}");
            }
            finally
            {
                Log.Close();
            }
        }

        void Run(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("KW1281Test: Yesterday's diagnostics...");
            Thread.Sleep(2000);
            Console.WriteLine("Today.");
            Thread.Sleep(2000);
            Console.ResetColor();
            Console.WriteLine();

            var version = GetType().GetTypeInfo().Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion;
            Log.WriteLine($"Version {version} (https://github.com/gmenounos/kw1281test/releases)");
            Log.WriteLine($"Args: {string.Join(' ', args)}");
            Log.WriteLine($"OSVersion: {Environment.OSVersion}");
            Log.WriteLine($".NET Version: {Environment.Version}");
            Log.WriteLine($"Culture: {CultureInfo.InstalledUICulture}");

            if (args.Length < 4)
            {
                ShowUsage();
                return;
            }

            // This seems to increase the accuracy of our timing loops
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            string portName = args[0];
            var baudRate = int.Parse(args[1]);
            int controllerAddress = int.Parse(args[2], NumberStyles.HexNumber);
            var command = args[3];
            uint address = 0;
            uint length = 0;
            byte value = 0;
            int softwareCoding = 0;
            int workshopCode = 0;
            byte channel = 0;
            ushort channelValue = 0;
            ushort? login = null;
            byte groupNumber = 0;

            if (string.Compare(command, "ReadEeprom", true) == 0)
            {
                if (args.Length < 5)
                {
                    ShowUsage();
                    return;
                }

                address = Utils.ParseUint(args[4]);
            }
            else if (string.Compare(command, "DumpMarelliMem", true) == 0 ||
                     string.Compare(command, "DumpEeprom", true) == 0 ||
                     string.Compare(command, "DumpMem", true) == 0 ||
                     string.Compare(command, "DumpRB8Eeprom", true) == 0)
            {
                if (args.Length < 6)
                {
                    ShowUsage();
                    return;
                }

                address = Utils.ParseUint(args[4]);
                length = Utils.ParseUint(args[5]);

                if (args.Length > 6)
                {
                    _filename = args[6];
                }
            }
            else if (string.Compare(command, "WriteEeprom", true) == 0)
            {
                if (args.Length < 6)
                {
                    ShowUsage();
                    return;
                }

                address = Utils.ParseUint(args[4]);
                value = (byte)Utils.ParseUint(args[5]);
            }
            else if (string.Compare(command, "LoadEeprom", true) == 0)
            {
                if (args.Length < 6)
                {
                    ShowUsage();
                    return;
                }

                address = Utils.ParseUint(args[4]);
                _filename = args[5];
            }
            else if (string.Compare(command, "SetSoftwareCoding", true) == 0)
            {
                if (args.Length < 6)
                {
                    ShowUsage();
                    return;
                }

                softwareCoding = (int)Utils.ParseUint(args[4]);
                if (softwareCoding > 32767)
                {
                    Log.WriteLine("SoftwareCoding cannot be greater than 32767.");
                    return;
                }
                workshopCode = (int)Utils.ParseUint(args[5]);
                if (workshopCode > 99999)
                {
                    Log.WriteLine("WorkshopCode cannot be greater than 99999.");
                    return;
                }
            }
            else if (string.Compare(command, "DumpEdc15Eeprom", true) == 0)
            {
                if (args.Length < 4)
                {
                    ShowUsage();
                    return;
                }

                if (args.Length > 4)
                {
                    _filename = args[4];
                }
            }
            else if (string.Compare(command, "AdaptationRead", true) == 0)
            {
                if (args.Length < 5)
                {
                    ShowUsage();
                    return;
                }

                channel = byte.Parse(args[4]);

                if (args.Length > 5)
                {
                    login = ushort.Parse(args[5]);
                }
            }
            else if (
                string.Compare(command, "AdaptationSave", true) == 0 ||
                string.Compare(command, "AdaptationTest", true) == 0)
            {
                if (args.Length < 6)
                {
                    ShowUsage();
                    return;
                }

                channel = byte.Parse(args[4]);
                channelValue = ushort.Parse(args[5]);

                if (args.Length > 6)
                {
                    login = ushort.Parse(args[6]);
                }
            }
            else if (
                string.Compare(command, "BasicSetting", true) == 0 ||
                string.Compare(command, "GroupRead", true) == 0)
            {
                if (args.Length < 5)
                {
                    ShowUsage();
                    return;
                }

                groupNumber = byte.Parse(args[4]);
            }

            using var @interface = OpenPort(portName, baudRate);
            var tester = new Tester(@interface, controllerAddress);

            ControllerInfo ecuInfo;
            switch (command.ToLower())
            {
                case "dumprb8eeprom":
                    tester.DumpRB8Eeprom(address, length, _filename);
                    break;

                case "getskc":
                    tester.GetSkc();
                    break;

                default:
                    break;
            }

            ecuInfo = tester.Kwp1281Wakeup();

            switch (command.ToLower())
            {
                case "actuatortest":
                    tester.ActuatorTest();
                    break;

                case "adaptationread":
                    tester.AdaptationRead(channel, login, ecuInfo.WorkshopCode);
                    break;

                case "adaptationsave":
                    tester.AdaptationSave(channel, channelValue, login, ecuInfo.WorkshopCode);
                    break;

                case "adaptationtest":
                    tester.AdaptationTest(channel, channelValue, login, ecuInfo.WorkshopCode);
                    break;

                case "basicsetting":
                    tester.BasicSettingRead(groupNumber);
                    break;

                case "clarionvwpremium4safecode":
                    tester.ClarionVWPremium4SafeCode();
                    break;

                case "clearfaultcodes":
                    tester.ClearFaultCodes();
                    break;

                case "delcovwpremium5safecode":
                    tester.DelcoVWPremium5SafeCode();
                    break;

                case "dumpccmrom":
                    tester.DumpCcmRom(_filename);
                    break;

                case "dumpclusternecrom":
                    tester.DumpClusterNecRom(_filename);
                    break;

                case "dumpedc15eeprom":
                    tester.DumpEdc15Eeprom(_filename);
                    break;

                case "dumpeeprom":
                    tester.DumpEeprom(address, length, _filename);
                    break;

                case "dumpmarellimem":
                    tester.DumpMarelliMem(address, length, ecuInfo, _filename);
                    return;

                case "dumpmem":
                    tester.DumpMem(address, length, _filename);
                    break;

                case "groupread":
                    tester.GroupRead(groupNumber);
                    break;

                case "loadeeprom":
                    tester.LoadEeprom(address, _filename!);
                    break;

                case "mapeeprom":
                    tester.MapEeprom(_filename);
                    break;

                case "readeeprom":
                    tester.ReadEeprom(address);
                    break;

                case "readfaultcodes":
                    tester.ReadFaultCodes();
                    break;

                case "readident":
                    tester.ReadIdent();
                    break;

                case "readsoftwareversion":
                    tester.ReadSoftwareVersion();
                    break;

                case "reset":
                    tester.Reset();
                    break;

                case "setsoftwarecoding":
                    tester.SetSoftwareCoding(softwareCoding, workshopCode);
                    break;

                case "writeeeprom":
                    tester.WriteEeprom(address, value);
                    break;

                default:
                    ShowUsage();
                    break;
            }

            tester.EndCommunication();
        }

        /// <summary>
        /// Opens the serial port.
        /// </summary>
        /// <param name="portName">
        /// Either the device name of a serial port (e.g. COM1, /dev/tty23)
        /// or an FTDI USB->Serial device serial number (2 letters followed by 6 letters/numbers).
        /// </param>
        /// <param name="baudRate"></param>
        /// <returns></returns>
        private static IInterface OpenPort(string portName, int baudRate)
        {
            if (Regex.IsMatch(portName.ToUpper(), @"\A[A-Z0-9]{8}\Z"))
            {
                Log.WriteLine($"Opening FTDI serial port {portName}");
                return new FtdiInterface(portName, baudRate);
            }
            else
            {
                Log.WriteLine($"Opening serial port {portName}");
                return new GenericInterface(portName, baudRate);
            }
        }

        private static void ShowUsage()
        {
            Log.WriteLine(@"Usage: KW1281Test PORT BAUD ADDRESS COMMAND [args]
    PORT = COM1|COM2|etc.
    BAUD = 10400|9600|etc.
    ADDRESS = The controller address, e.g. 1 (ECU), 17 (cluster), 46 (CCM), 56 (radio)
    COMMAND =
        ActuatorTest
        AdaptationRead CHANNEL [LOGIN]
            CHANNEL = Channel number (0-99)
            LOGIN = Optional login (0-65535)
        AdaptationSave CHANNEL VALUE [LOGIN]
            CHANNEL = Channel number (0-99)
            VALUE = Channel value (0-65535)
            LOGIN = Optional login (0-65535)
        AdaptationTest CHANNEL VALUE [LOGIN]
            CHANNEL = Channel number (0-99)
            VALUE = Channel value (0-65535)
            LOGIN = Optional login (0-65535)
        BasicSetting GROUP
            GROUP = Group number (0-255)
            (Group 0: Raw controller data)
        ClarionVWPremium4SafeCode
        ClearFaultCodes
        DelcoVWPremium5SafeCode
        DumpEdc15Eeprom [FILENAME]
            FILENAME = Optional filename
        DumpEeprom START LENGTH [FILENAME]
            START = Start address in decimal (e.g. 0) or hex (e.g. $0)
            LENGTH = Number of bytes in decimal (e.g. 2048) or hex (e.g. $800)
            FILENAME = Optional filename
        DumpMarelliMem START LENGTH [FILENAME]
            START = Start address in decimal (e.g. 3072) or hex (e.g. $C00)
            LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. $400)
            FILENAME = Optional filename
        DumpMem START LENGTH [FILENAME]
            START = Start address in decimal (e.g. 8192) or hex (e.g. $2000)
            LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. $10000)
            FILENAME = Optional filename
        DumpRB8Eeprom START LENGTH [FILENAME]
            START = Start address in decimal (e.g. 66560) or hex (e.g. $10400)
            LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. $400)
            FILENAME = Optional filename
        GetSKC
        GroupRead GROUP
            GROUP = Group number (0-255)
            (Group 0: Raw controller data)
        LoadEeprom START FILENAME
            START = Start address in decimal (e.g. 0) or hex (e.g. $0)
            FILENAME = Name of file containing binary data to load into EEPROM
        MapEeprom
        ReadFaultCodes
        ReadIdent
        ReadEeprom ADDRESS
            ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. $1109)
        ReadSoftwareVersion
        Reset
        SetSoftwareCoding CODING WORKSHOP
            CODING = Software coding in decimal (e.g. 4361) or hex (e.g. $1109)
            WORKSHOP = Workshop code in decimal (e.g. 4361) or hex (e.g. $1109)
        WriteEeprom ADDRESS VALUE
            ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. $1109)
            VALUE = Value in decimal (e.g. 138) or hex (e.g. $8A)");
        }

        private string? _filename = null;
    }
}
