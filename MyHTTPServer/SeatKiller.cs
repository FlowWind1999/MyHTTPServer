using System;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Newtonsoft.Json.Linq;

namespace MyHTTPServer
{
    public class SeatKiller
    {
        private const string API_ROOT = "https://seat.lib.whu.edu.cn:8443/rest/";
        private const string API_V2_ROOT = "https://seat.lib.whu.edu.cn:8443/rest/v2/";
        private const string API_UPDATE = "https://api.github.com/repos/yeliudev/Seatkiller-GUI/releases/latest";
        private const string SERVER = "134.175.186.17";

        public static readonly string[] xt_lite = { "9", "11", "6", "7", "8", "10", "16" };
        public static readonly string[] xt = { "6", "7", "8", "9", "10", "11", "12", "16", "4", "5", "14", "15" };
        public static readonly string[] gt = { "19", "29", "31", "32", "33", "34", "35", "37", "38" };
        public static readonly string[] yt = { "20", "21", "23", "24", "26", "27" };
        public static readonly string[] zt = { "39", "40", "51", "52", "56", "59", "60", "61", "62", "65", "66", "84", "85", "86", "87", "88", "89", "92" };

        public ArrayList freeSeats = new ArrayList();
        private ArrayList startTimes = new ArrayList(), endTimes = new ArrayList();
        public string to_addr, res_id, username, password, newVersion, newVersionSize, updateInfo, downloadURL, status, bookedSeatId, historyDate, historyStartTime, historyEndTime, historyAwayStartTime, token, name, last_login_time, state, violationCount;
        public bool checkedIn, reserving, onlyPower, onlyWindow, onlyComputer;
        public DateTime time;
       

        public void Wait(string hour, string minute, string second, bool enter = true)
        {
            time = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd") + " " + hour + ":" + minute + ":" + second);
            if (DateTime.Compare(DateTime.Now, time) > 0)
            {
                time = time.AddDays(1);
            }
            
            while (true)
            {
                TimeSpan delta = time.Subtract(DateTime.Now);
                if (delta.TotalSeconds < 0)
                {
                   
                    break;
                }
                Thread.Sleep(5);
            }
            return;
        }

        //登陆用的
        public string GetToken(bool alert = true)
        {
            string url = API_ROOT + "auth?username=" + username + "&password=" + password;
            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);
                if (res["status"].ToString() == "success")
                {
                    token = res["data"]["token"].ToString();                //这个要给回客户端
                    return "Success";
                }
                else
                {
                    return res["message"].ToString();
                }
            }
            catch
            {
                return "Connection lost";
            }
        }

        //查看现在是否有预约
        public bool CheckResInf(bool alert = true, bool modal = false)      
        {
            string url = API_V2_ROOT + "history/1/30";
            string[] probableStatus = { "RESERVE", "CHECK_IN", "AWAY" };
            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token, 2000);

                return true;
            }
            catch
            {
                return false;
            }
        }


        public bool GetUsrInf(bool alert = true)
        {
            string url = API_V2_ROOT + "user";

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);


                if (res["status"].ToString() == "success")
                {
                    name = res["data"]["name"].ToString();
                    last_login_time = res["data"]["lastLogin"].ToString();
                    if (res["data"]["checkedIn"].ToString() == "True")
                    {
                        checkedIn = true;
                        state = "已进入" + res["data"]["lastInBuildingName"].ToString();
                    }
                    else
                    {
                        checkedIn = false;
                        state = "未入馆";
                    }

                    violationCount = res["data"]["violationCount"].ToString();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }


        public bool GetRooms(string buildingId)
        {
            string url = API_V2_ROOT + "room/stats2/" + buildingId;
            Console.WriteLine("\r\nFetching room info.....");

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);
                Console.WriteLine(res["status"].ToString());

                if (res["status"].ToString() == "success")
                {
                    JToken jToken = res["data"];
                    Console.WriteLine("\r\n\r\n当前座位状态：");

                    foreach (var room in jToken)
                    {
                        Console.WriteLine("\r\n\r\n" + room["room"].ToString() + "\r\n楼层：" + room["floor"].ToString() + "\r\n总座位数：" + room["totalSeats"].ToString() + "\r\n已预约：" + room["reserved"].ToString() + "\r\n正在使用：" + room["inUse"].ToString() + "\r\n暂离：" + room["away"].ToString() + "\r\n空闲：" + room["free"].ToString());
                    }
                    Console.WriteLine("\r\n");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                Console.WriteLine("Connection lost");
                return false;
            }
        }

        //获取指定区域的可用座位
        public bool GetSeats(string roomId, ArrayList seats)
        {
            string url = API_V2_ROOT + "room/layoutByDate/" + roomId + "/" + DateTime.Now.ToString("yyyy-MM-dd");
            Console.WriteLine("\r\nFetching seat info in room " + roomId + ".....");

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);           //直接回传给客户端
                Console.WriteLine(res["status"].ToString());

                if (res["status"].ToString() == "success")
                {
                    JToken layout = res["data"]["layout"];
                    foreach (var num in layout)
                    {
                        if (num.First["type"].ToString() == "seat")
                        {
                            string seatInfo = num.First["name"].ToString();
                            if (num.First["power"].ToString() == "True")
                                seatInfo += " (电源)";
                            if (num.First["window"].ToString() == "True")
                                seatInfo += " (靠窗)";
                            if (num.First["computer"].ToString() == "True")
                                seatInfo += " (电脑)";
                            seats.Add(new DictionaryEntry(num.First["id"].ToString(), seatInfo));
                        }
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine("\r\n" + res.ToString());
                    return false;
                }
            }
            catch
            {
                Console.WriteLine("Connection lost");
                return false;
            }
        }

        public string GetSeats(string roomId)
        {
            string url = API_V2_ROOT + "room/layoutByDate/" + roomId + "/" + DateTime.Now.ToString("yyyy-MM-dd");
            Console.WriteLine("\r\nFetching seat info in room " + roomId + ".....");

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);           //直接回传给客户端
                Console.WriteLine(res["status"].ToString());
                return res.ToString();
            }
            catch
            {
                Console.WriteLine("Connection lost");
                return "Connection lost";
            }
        }

        //取消预约
        public bool CancelReservation(string id, bool alert = true)
        {
            string url = API_V2_ROOT + "cancel/" + id;
            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);

                if (res["status"].ToString() == "success")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool StopUsing(bool alert = true)
        {
            string url = API_V2_ROOT + "stop";

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);

                if (res["status"].ToString() == "success")
                {
                    return true;
                }
                else
                {
                    
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public string SearchFreeSeat(string buildingId, string roomId, string date, string startTime, string endTime)
        {
            if (startTime == "-1")
            {
                startTime = ((int)DateTime.Now.TimeOfDay.TotalMinutes).ToString();
            }

            string url = API_V2_ROOT + "searchSeats/" + date + "/" + startTime + "/" + endTime;
            //Config.config.textBox2.AppendText("\r\nFetching free seats in room " + roomId + ".....");

            StringBuilder buffer = new StringBuilder();
            buffer.AppendFormat("{0}={1}", "t", "1");
            buffer.AppendFormat("&{0}={1}", "roomId", roomId);
            buffer.AppendFormat("&{0}={1}", "buildingId", buildingId);
            buffer.AppendFormat("&{0}={1}", "batch", "9999");
            buffer.AppendFormat("&{0}={1}", "page", "1");
            buffer.AppendFormat("&{0}={1}", "t2", "2");
            byte[] data = Encoding.UTF8.GetBytes(buffer.ToString());

            try
            {
                JObject res = HTTPRequest.HttpPostRequest(url, token, data);

                if (res["data"]["seats"].ToString() != "{}")
                {
                    JToken seats = res["data"]["seats"];
                    foreach (var num in seats)
                    {
                        if (onlyPower && num.First["power"].ToString() == "False")
                        {
                            continue;
                        }
                        if (onlyWindow && num.First["window"].ToString() == "False")
                        {
                            continue;
                        }
                        if (onlyComputer && num.First["computer"].ToString() == "False")
                        {
                            continue;
                        }
                        freeSeats.Add(num.First["id"].ToString());
                    }
                    if (freeSeats.Count > 0)
                    {
                        //Config.config.textBox2.AppendText("success");
                        return "Success";
                    }
                    else
                    {
                        //Config.config.textBox2.AppendText("fail");
                        return "Failed";
                    }
                }
                else
                {
                    //Config.config.textBox2.AppendText("fail");
                    return "Failed";
                }
            }
            catch
            {
                //Config.config.textBox2.AppendText("Connection lost");
                return "Connection lost";
            }
        }

        public bool CheckStartTime(string seatId, string date, string startTime)
        {
            if (startTime == "-1")
            {
                startTime = "now";
            }

            string url = API_V2_ROOT + "startTimesForSeat/" + seatId + "/" + date;
            //Config.config.textBox2.AppendText("\r\nChecking start time of seat No." + seatId + ".....");

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);

                if (res["status"].ToString() == "success")
                {
                    startTimes.Clear();
                    JToken getStartTimes = res["data"]["startTimes"];
                    foreach (var time in getStartTimes)
                    {
                        startTimes.Add((time["id"].ToString()));
                    }

                    if (startTimes.Contains(startTime))
                    {
                        //Config.config.textBox2.AppendText("success");
                        return true;
                    }
                    else
                    {
                        //Config.config.textBox2.AppendText("fail");
                        return false;
                    }
                }
                else
                {
                    //Config.config.textBox2.AppendText("fail");
                    return false;
                }
            }
            catch
            {
                //Config.config.textBox2.AppendText("Connection lost");
                return false;
            }
        }

        public bool CheckEndTime(string seatId, string date, string startTime, string endTime)
        {
            if (startTime == "-1")
            {
                startTime = ((int)DateTime.Now.TimeOfDay.TotalMinutes).ToString();
            }

            string url = API_V2_ROOT + "endTimesForSeat/" + seatId + "/" + date + "/" + startTime;
            //Config.config.textBox2.AppendText("\r\nChecking end time of seat No." + seatId + ".....");

            try
            {
                JObject res = HTTPRequest.HttpGetRequest(url, token);

                if (res["status"].ToString() == "success")
                {
                    endTimes.Clear();
                    JToken getEndTimes = res["data"]["endTimes"];
                    foreach (var time in getEndTimes)
                    {
                        endTimes.Add((time["id"].ToString()));
                    }

                    if (endTimes.Contains(endTime))
                    {
                        //Config.config.textBox2.AppendText("success");
                        return true;
                    }
                    else
                    {
                        //Config.config.textBox2.AppendText("fail");
                        return false;
                    }
                }
                else
                {
                    //Config.config.textBox2.AppendText("fail");
                    return false;
                }
            }
            catch
            {
                //Config.config.textBox2.AppendText("Connection lost");
                return false;
            }
        }

        public string BookSeat(string seatId, string date, string startTime, string endTime, bool alert = true)
        {
            string url = API_V2_ROOT + "freeBook";

            StringBuilder buffer = new StringBuilder();
            buffer.AppendFormat("{0}={1}", "t", "1");
            buffer.AppendFormat("&{0}={1}", "startTime", startTime);
            buffer.AppendFormat("&{0}={1}", "endTime", endTime);
            buffer.AppendFormat("&{0}={1}", "seat", seatId);
            buffer.AppendFormat("&{0}={1}", "date", date);
            buffer.AppendFormat("&{0}={1}", "t2", "2");
            byte[] data = Encoding.UTF8.GetBytes(buffer.ToString());
    
            try
            {
                JObject res = HTTPRequest.HttpPostRequest(url, token, data);

                if (res["status"].ToString() == "success")
                {
                    bookedSeatId = seatId;
                    if (alert)
                    {
                        res.Remove("status");
                        res.Remove("message");
                        res.Remove("code");     
                    }
                    return "Success";
                }
                else
                {               
                    return "Failed";
                }
            }
            catch
            {               
                return "Connection lost";
            }
        }
        

        public void LockSeat(string seatId)
        {
            int index, linesCount, count = 0;
            bool doClear = false, reBook = false;
            Console.WriteLine("\r\n正在锁定座位，ID: " + seatId + "\r\n");
            if (!CheckResInf(false))
            {
                Console.WriteLine("\r\n\r\n预约信息获取失败");
                return;
            }
            while (true)
            {
                if (count >= 50)
                {
                    Console.WriteLine("\r\n\r\n座位锁定失败");
                    break;
                }

                if (historyDate == DateTime.Now.ToString("yyyy-M-d") && DateTime.Now.TimeOfDay.TotalMinutes > 400 && DateTime.Now.TimeOfDay.TotalMinutes < 1320)
                {
                    if (GetToken(false) == "Success")
                    {
                        if (CheckResInf(false) || reBook)
                        {
                            int historyEndTimeInt = int.Parse(historyEndTime.Substring(0, 2)) * 60 + int.Parse(historyEndTime.Substring(3, 2));

                            if (historyEndTimeInt - (int)DateTime.Now.TimeOfDay.TotalMinutes < 2)
                            {
                                if (reserving)
                                {
                                    Console.WriteLine("\r\n\r\n座位预约时间已过，自动取消预约");
                                    CancelReservation(res_id);
                                }
                                else
                                {
                                    Console.WriteLine("\r\n\r\n座位预约时间已过，自动释放座位");
                                    StopUsing();
                                }
                                break;
                            }

                            if (reserving && !checkedIn)
                            {
                                int historyStartTimeInt = int.Parse(historyStartTime.Substring(0, 2)) * 60 + int.Parse(historyStartTime.Substring(3, 2));

                                if ((int)DateTime.Now.TimeOfDay.TotalMinutes - historyStartTimeInt >= 25)
                                {
                                    if (CancelReservation(res_id, false) || reBook)
                                    {
                                        if (BookSeat(seatId, DateTime.Now.ToString("yyyy-MM-dd"), "-1", historyEndTimeInt.ToString(), false) != "Success")
                                        {
                                            if (doClear)
                                            {
                                                //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                                                //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                                                //Config.config.textBox2.SelectedText = "重新预约座位失败，重试次数: " + count;
                                            }
                                            else
                                            {
                                                //Config.config.textBox2.AppendText("\r\n重新预约座位失败，重试次数: " + count);
                                                doClear = true;
                                            }
                                            Thread.Sleep(5000);
                                            reBook = true;
                                            count += 1;
                                            continue;
                                        }
                                        else
                                        {
                                            reBook = false;
                                        }
                                    }
                                    else
                                    {
                                        if (doClear)
                                        {
                                            //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                                            //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                                            //Config.config.textBox2.SelectedText = "取消预约失败，重试次数: " + count;
                                        }
                                        else
                                        {
                                            //Config.config.textBox2.AppendText("\r\n取消预约失败，重试次数: " + count);
                                            doClear = true;
                                        }
                                        Thread.Sleep(5000);
                                        count += 1;
                                        continue;
                                    }
                                }
                            }
                            else if (status == "AWAY")
                            {
                                int historyAwayStartTimeInt = int.Parse(historyAwayStartTime.Substring(0, 2)) * 60 + int.Parse(historyAwayStartTime.Substring(3, 2));

                                if ((int)DateTime.Now.TimeOfDay.TotalMinutes - historyAwayStartTimeInt >= 25)
                                {
                                    if (StopUsing(false) || reBook)
                                    {
                                        if (BookSeat(seatId, DateTime.Now.ToString("yyyy-MM-dd"), "-1", historyEndTimeInt.ToString(), false) != "Success")
                                        {
                                            if (doClear)
                                            {
                                                //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                                                //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                                                //Config.config.textBox2.SelectedText = "重新预约座位失败，重试次数: " + count;
                                            }
                                            else
                                            {
                                                //Config.config.textBox2.AppendText("\r\n重新预约座位失败，重试次数: " + count);
                                                doClear = true;
                                            }
                                            Thread.Sleep(5000);
                                            reBook = true;
                                            count += 1;
                                            continue;
                                        }
                                        else
                                        {
                                            reBook = false;
                                        }
                                    }
                                    else
                                    {
                                        if (doClear)
                                        {
                                            //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                                            //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                                            //Config.config.textBox2.SelectedText = "释放座位失败，重试次数: " + count;
                                        }
                                        else
                                        {
                                            //Config.config.textBox2.AppendText("\r\n释放座位失败，重试次数: " + count);
                                            doClear = true;
                                        }
                                        Thread.Sleep(5000);
                                        count += 1;
                                        continue;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (doClear)
                            {
                                //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                                //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                                //Config.config.textBox2.SelectedText = "获取预约信息失败，重试次数: " + count;
                            }
                            else
                            {
                                //Config.config.textBox2.AppendText("\r\n获取预约信息失败，重试次数: " + count);
                                doClear = true;
                            }
                            Thread.Sleep(5000);
                            count += 1;
                            continue;
                        }
                    }
                    else
                    {
                        if (doClear)
                        {
                            //index = Config.config.textBox2.GetFirstCharIndexOfCurrentLine();
                            //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                            //Config.config.textBox2.SelectedText = "获取token失败，重试次数: " + count;
                        }
                        else
                        {
                            //Config.config.textBox2.AppendText("\r\n获取token失败，重试次数: " + count);
                            doClear = true;
                        }
                        Thread.Sleep(5000);
                        count += 1;
                        continue;
                    }
                }
                else if (historyDate == DateTime.Now.ToString("yyyy-M-d") && DateTime.Now.TimeOfDay.TotalMinutes > 1320)
                {
                    return;
                }
                count = 0;
                //linesCount = Config.config.textBox2.Lines.Count();
                //index = Config.config.textBox2.GetFirstCharIndexFromLine(linesCount - (doClear ? 2 : 1));
                //Config.config.textBox2.Select(index, Config.config.textBox2.TextLength - index);
                //Config.config.textBox2.SelectedText = "当前有效" + (reserving ? "预约" : "使用") + "时间: " + historyDate + " " + historyStartTime + "~" + historyEndTime;
                doClear = false;
                Thread.Sleep(30000);
            }
        }

        public bool Loop(string buildingId, string[] rooms, string startTime, string endTime, string roomId = "0", string seatId = "0")
        {
            Console.WriteLine("\r\n\r\n---------------------------进入捡漏模式---------------------------\r\n");

            if (DateTime.Now.TimeOfDay.TotalMinutes < 60 || DateTime.Now.TimeOfDay.TotalMinutes > 1420)
            {
                Wait("01", "00", "00", false);
            }
            else if (DateTime.Now.TimeOfDay.TotalMinutes > 1320)
            {
                Console.WriteLine("\r\n捡漏失败，超出系统开放时间\r\n");
                Console.WriteLine("\r\n---------------------------退出捡漏模式---------------------------\r\n");
                return false;
            }

            string date = DateTime.Now.ToString("yyyy-MM-dd");
            GetRooms(buildingId);

            while (true)
            {
                if (DateTime.Now.TimeOfDay.TotalMinutes > 1320)
                {
                    Console.WriteLine("\r\n\r\n捡漏失败，超出系统开放时间\r\n");
                    Console.WriteLine("\r\n---------------------------退出捡漏模式---------------------------\r\n");
                    return false;
                }

                if (startTime != "-1" && (int)DateTime.Now.TimeOfDay.TotalMinutes > int.Parse(startTime))
                {
                    startTime = "-1";
                }

                if (seatId != "0")          //  不是随机的
                {
                    string res = BookSeat(seatId, date, startTime, endTime);

                    if (res == "Success")
                    {
                        Console.WriteLine("\r\n\r\n捡漏成功\r\n");
                        Console.WriteLine("\r\n---------------------------退出捡漏模式---------------------------\r\n");
                        return true;
                    }
                    else if (res == "Connection lost")
                    {
                        Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续预约空位\r\n");
                        Thread.Sleep(30000);
                        continue;
                    }
                }
                else
                {
                    freeSeats.Clear();

                    if (roomId == "0")
                    {
                        foreach (var room in rooms)
                        {
                            string res = SearchFreeSeat(buildingId, room, date, startTime, endTime);
                            if (res == "Success")
                            {
                                break;
                            }
                            else if (res == "Connection lost")
                            {
                                Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续检索空位\r\n");
                                Thread.Sleep(30000);
                                continue;
                            }
                            Thread.Sleep(1500);
                        }
                    }
                    else
                    {
                        string res = SearchFreeSeat(buildingId, roomId, date, startTime, endTime);
                        if (res == "Connection lost")
                        {
                            Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续检索空位\r\n");
                            Thread.Sleep(30000);
                            continue;
                        }
                        
                        
                    }

                    foreach (var freeSeatId in freeSeats)
                    {
                        switch (BookSeat(freeSeatId.ToString(), date, startTime, endTime))
                        {
                            case "Success":
                                Console.WriteLine("\r\n\r\n捡漏成功\r\n");
                                Console.WriteLine("\r\n---------------------------退出捡漏模式---------------------------\r\n");
                                return true;
                            case "Failed":
                                Thread.Sleep(1500);
                                break;
                            case "Connection lost":
                                Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续预约空位\r\n");
                                Thread.Sleep(30000);
                                break;
                        }
                    }
                }
                Console.WriteLine("\r\n\r\n暂无可用座位，系统开放时间剩余" + (79200 - (int)DateTime.Now.TimeOfDay.TotalSeconds).ToString() + "秒\r\n");
                Thread.Sleep(1500);
            }
        }

        public bool ExchangeLoop(string buildingId, string[] rooms, string startTime, string endTime, string roomId = "0", string seatId = "0")
        {
            Console.WriteLine("\r\n\r\n---------------------------进入改签模式---------------------------\r\n");

            if (DateTime.Now.TimeOfDay.TotalMinutes < 60 || DateTime.Now.TimeOfDay.TotalMinutes > 1420)
            {
                Wait("01", "00", "00", false);
            }
            else if (DateTime.Now.TimeOfDay.TotalMinutes > 1320)
            {
                Console.WriteLine("\r\n改签失败，超出系统开放时间\r\n");
                Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                return false;
            }

            bool cancelled = false;
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            GetRooms(buildingId);

            

            while (true)
            {
                if (DateTime.Now.TimeOfDay.TotalMinutes > 1320)
                {
                    Console.WriteLine("\r\n\r\n改签失败，超出系统开放时间\r\n");
                    Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                    return false;
                }

                if (startTime != "-1" && (int)DateTime.Now.TimeOfDay.TotalMinutes > int.Parse(startTime))
                {
                    startTime = "-1";
                }

                if (seatId != "0")
                {
                    if (CheckStartTime(seatId, date, startTime) && CheckEndTime(seatId, date, startTime, endTime))
                    {
                        GetUsrInf(true);
                        if (!reserving)
                        {
                            if (StopUsing())
                            {
                                if (BookSeat(seatId, date, startTime, endTime) == "Success")
                                {
                                    Console.WriteLine("\r\n\r\n改签成功\r\n");
                                    Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                                    return true;
                                }
                                else
                                {
                                    Console.WriteLine("\r\n\r\n改签失败，原座位已丢失\r\n");
                                    Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                                    return false;
                                }
                            }
                            else
                            {
                                Console.WriteLine("\r\n\r\n---------------------------退出改签模式---------------------------\r\n");
                                return false;
                            }
                        }
                        else
                        {
                            if (CancelReservation(res_id))
                            {
                                if (BookSeat(seatId, date, startTime, endTime) == "Success")
                                {
                                    Console.WriteLine("\r\n\r\n改签成功\r\n");
                                    Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                                    return true;
                                }
                                else
                                {
                                    Console.WriteLine("\r\n\r\n改签失败，原座位已丢失\r\n");
                                    Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                                    return false;
                                }
                            }
                            else
                            {
                                Console.WriteLine("\r\n\r\n---------------------------退出改签模式---------------------------\r\n");
                                return false;
                            }
                        }
                    }
                    
                }
                else
                {
                    freeSeats.Clear();

                    if (roomId == "0")
                    {
                        foreach (var room in rooms)
                        {
                            string res = SearchFreeSeat(buildingId, room, date, startTime, endTime);
                            if (res == "Success")
                            {
                                break;
                            }
                            else if (res == "Connection lost")
                            {
                                Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续检索空位\r\n");
                                Thread.Sleep(30000);
                                continue;
                            }
                            Thread.Sleep(1500);
                        }
                    }
                    else
                    {
                        string res = SearchFreeSeat(buildingId, roomId, date, startTime, endTime);
                        if (res == "Connection lost")
                        {
                            Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续检索空位\r\n");
                            Thread.Sleep(30000);
                            continue;
                        }
                    }

                    foreach (var freeSeatId in freeSeats)
                    {
                        if (!cancelled)
                        {
                            if (CheckStartTime(freeSeatId.ToString(), date, startTime) && CheckEndTime(freeSeatId.ToString(), date, startTime, endTime))
                            {
                                GetUsrInf(true);
                                if (!reserving)
                                {
                                    if (StopUsing())
                                    {
                                        cancelled = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine("\r\n\r\n---------------------------退出改签模式---------------------------\r\n");
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (CancelReservation(res_id))
                                    {
                                        cancelled = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine("\r\n\r\n---------------------------退出改签模式---------------------------\r\n");
                                        return false;
                                    }
                                }

                                switch (BookSeat(freeSeatId.ToString(), date, startTime, endTime))
                                {
                                    case "Success":
                                        Console.WriteLine("\r\n\r\n改签成功\r\n");
                                        Console.WriteLine("\r\n---------------------------退出改签模式---------------------------\r\n");
                                        return true;
                                    case "Failed":
                                        Thread.Sleep(1500);
                                        break;
                                    case "Connection lost":
                                        Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续预约空位\r\n");
                                        Thread.Sleep(30000);
                                        break;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("\r\n\r\n暂无可用座位，系统开放时间剩余" + (79200 - (int)DateTime.Now.TimeOfDay.TotalSeconds).ToString() + "秒\r\n");
                Thread.Sleep(1500);
            }
        }

    }
}