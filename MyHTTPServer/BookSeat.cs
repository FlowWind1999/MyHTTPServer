using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Threading;

namespace MyHTTPServer
{
    class BookSeat
    {
        public string buildingId;
        public string roomId;
        public string seatId;
        public string date;
        public string startTime;
        public string endTime;

        public string[] rooms;
        public bool enter;
        private Thread thread;

        public string Login(string username, string password)
        {
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.username = username;
            seatKiller.password = password;
            string response = seatKiller.GetToken(false);          //登陆
            Console.WriteLine(response);
            Console.WriteLine(seatKiller.token);
            return response;
        }

        public void CheckResInf(string token)
        {
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.token = token;
            seatKiller.CheckResInf();
        }

        public void GetSeats(string roomID, string token)
        {
            ArrayList seats = new ArrayList();
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.token = token;
            seatKiller.GetSeats(roomID, seats);

        }

        public string GetSeats(string roomID, string username, string password)
        {
            ArrayList seats = new ArrayList();
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.username = username;
            seatKiller.password = password;
            seatKiller.GetToken(false);
            string res = seatKiller.GetSeats(roomID);
            return res;
        }

        public string BookS(string username, string password)
        {
            ArrayList seats = new ArrayList();
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.username = username;
            seatKiller.password = password;
            string res = "";
            if (seatKiller.GetToken() == "Success")
            {
                res = seatKiller.BookSeat(seatId, date, startTime, endTime);
            }
            return res;
        }

        public void Book(string username, string password)
        {
            ArrayList seats = new ArrayList();
            SeatKiller seatKiller = new SeatKiller();
            seatKiller.username = username;
            seatKiller.password = password;

            bool cancelled = false;
            bool exchange = false;          //有预约，改签

            bool today = true;

            if (today == false)     //明天的
            {
                Console.WriteLine("\r\n---------------------------进入抢座模式---------------------------\r\n");

                if (DateTime.Now.TimeOfDay.TotalSeconds < 81880)
                {
                    seatKiller.Wait("22", "44", "40", false);
                }

                if (seatKiller.GetToken() == "Success")         //登陆验证
                {
                    if (seatKiller.CheckResInf(false))
                    {
                        Console.WriteLine("\r\n已检测到有效预约，将自动改签预约信息\r\n");
                        exchange = true;
                    }
                    else
                    {
                        exchange = false;
                    }

                    if (DateTime.Now.TimeOfDay.TotalMinutes < 1365)         //22点45分，要在这个时间之后才可以抢明天的座位
                    {
                        seatKiller.GetRooms(buildingId);
                        seatKiller.Wait("22", "45", "00");
                    }
                    else if (DateTime.Now.TimeOfDay.TotalMinutes > 1420)
                    {
                        Console.WriteLine("\r\n预约系统已关闭");

                        if (exchange)
                        {
                            if (seatKiller.ExchangeLoop(buildingId, rooms, startTime, endTime, roomId, seatId))
                            {
                                seatKiller.LockSeat(seatKiller.bookedSeatId);
                            }
                        }
                        else
                        {
                            if (seatKiller.Loop(buildingId, rooms, startTime, endTime, roomId, seatId))
                            {
                                seatKiller.LockSeat(seatKiller.bookedSeatId);
                            }
                        }
                        return;
                    }


                    while (true)
                    {
                        if (DateTime.Now.TimeOfDay.TotalMinutes > 1420)
                        {
                            Console.WriteLine("\r\n\r\n抢座失败，座位预约系统已关闭");

                            if (exchange)
                            {
                                if (seatKiller.ExchangeLoop(buildingId, rooms, startTime, endTime, roomId, seatId))
                                {
                                    seatKiller.LockSeat(seatKiller.bookedSeatId);
                                }
                            }
                            else
                            {
                                if (seatKiller.Loop(buildingId, rooms, startTime, endTime, roomId, seatId))
                                {
                                    seatKiller.LockSeat(seatKiller.bookedSeatId);
                                }
                            }

                            return;
                        }

                        // 不随机座位
                        if (seatId != "0")
                        {
                            if (exchange && !seatKiller.reserving && !cancelled)
                            {
                                if (seatKiller.StopUsing())
                                {
                                    cancelled = true;
                                }
                                else
                                {
                                    Console.WriteLine("\r\n\r\n释放座位失败，请稍后重试\r\n");
                                    Console.WriteLine("\r\n---------------------------退出抢座模式---------------------------\r\n");

                                    return;
                                }
                            }
                            else if (exchange && seatKiller.reserving && !cancelled)
                            {
                                if (seatKiller.CancelReservation(seatKiller.res_id))
                                {
                                    cancelled = true;
                                }
                                else
                                {
                                    Console.WriteLine("\r\n\r\n取消预约失败，请稍后重试\r\n");
                                    Console.WriteLine("\r\n---------------------------退出抢座模式---------------------------\r\n");
                                    return;
                                }
                            }

                            string res = seatKiller.BookSeat(seatId, date, startTime, endTime);         //这一句是最重要的抢座

                            if (res == "Success")
                            {
                                Console.WriteLine("\r\n\r\n---------------------------退出抢座模式---------------------------\r\n");
                                seatKiller.LockSeat(seatKiller.bookedSeatId);

                                return;
                            }
                            else if (res == "Connection lost")
                            {
                                Console.WriteLine("\r\n\r\n连接丢失，30秒后尝试继续预约空位\r\n");
                                Thread.Sleep(30000);
                                continue;
                            }

                        }

                        Console.WriteLine("\r\n\r\n暂无可用座位，系统开放时间剩余" + (85200 - (int)DateTime.Now.TimeOfDay.TotalSeconds).ToString() + "秒\r\n");
                        Thread.Sleep(1500);
                    }
                }
                else
                {
                    Console.WriteLine("\r\n\r\n获取token失败，请检查网络后重试\r\n");
                    Console.WriteLine("\r\n---------------------------退出抢座模式---------------------------\r\n");
                    return;
                }
            }
            else
            {
                if (seatKiller.GetToken(false) == "Success")
                {
                    if (seatKiller.CheckResInf(false))
                    {
                        Console.WriteLine("\r\n\r\n已检测到有效预约，将自动改签预约信息");
                        if (seatKiller.ExchangeLoop(buildingId, rooms, startTime, endTime, roomId, seatId))
                        {
                            seatKiller.LockSeat(seatKiller.bookedSeatId);
                        }
                    }
                    else
                    {
                        if (seatKiller.Loop(buildingId, rooms, startTime, endTime, roomId, seatId))
                        {
                            seatKiller.LockSeat(seatKiller.bookedSeatId);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\r\n\r\n获取token失败，请检查网络后重试\r\n");
                }
                return;
            }
        }
    }
}
