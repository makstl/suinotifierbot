using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot.Rpc
{
	internal class RpcException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }

        public RpcException(HttpStatusCode code, string message) : base(message) => StatusCode = code;
    }
}
