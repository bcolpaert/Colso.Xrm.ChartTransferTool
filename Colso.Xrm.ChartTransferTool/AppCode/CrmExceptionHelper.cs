﻿using System;
using System.ServiceModel;

namespace Colso.Xrm.ChartTransferTool.AppCode
{
    public class CrmExceptionHelper
    {
        public static string GetErrorMessage(Exception error, bool returnWithStackTrace)
        {
            if (error.InnerException is FaultException)
            {
                if (returnWithStackTrace)
                {
                    return (error.InnerException).ToString();
                }
                else
                {
                    return (error.InnerException).Message;
                }
            }
            else if (error.InnerException != null)
            {
                if (returnWithStackTrace)
                {
                    return error.InnerException.ToString();
                }
                else
                {
                    return error.InnerException.Message;
                }
            }
            else
            {
                if (returnWithStackTrace)
                {
                    return error.ToString();
                }
                else
                {
                    return error.Message;
                }
            }
        }
    }
}