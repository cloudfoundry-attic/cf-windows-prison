using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.ComWrapper
{
    [ComVisible(true)]
    public interface IContainerManager
    {
        [ComVisible(true)]
        string[] ListContainerIds();

        [ComVisible(true)]
        IContainer GetContainerById(string id);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class ContainerManager : IContainerManager
    {
        public string[] ListContainerIds()
        {
            var res = new List<string>();
            var all = Prison.Load();
            foreach (var p in all)
            {
                res.Add(p.Id.ToString());
            }

            return res.ToArray();
        }

        public IContainer GetContainerById(string id)
        {
            Container tempResult = null;
            Container result = null;

            try
            {
                var prison = Prison.LoadPrisonAndAttach(new Guid(id));
                if (prison == null)
                {
                    return null;
                }

                tempResult = new Container(prison);
                result = tempResult;
                tempResult = null;
            }
            finally
            {
                if (result != null)
                {
                    result.Dispose();
                }
            }

            return result;
        }

        public void DestroyContainer(string id)
        {
            var p = Prison.LoadPrisonAndAttach(new Guid(id));
            if (p == null)
            {
                throw new ArgumentException("Container ID not found");
            }

            p.Destroy();
        }
    }
}
