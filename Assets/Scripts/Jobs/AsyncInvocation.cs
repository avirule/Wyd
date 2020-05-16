#region

using System;
using System.Threading.Tasks;

#endregion

namespace Wyd.Jobs
{
        /// <summary>
        ///     This delegate is used as an effective asynchronous counterpart to the <see cref="Action" />.
        /// </summary>
        public delegate Task AsyncInvocation();
}
