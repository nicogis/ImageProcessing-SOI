namespace Studioat.ArcGis.Soi.ImageProcessing
{
    using System;
    using System.Collections;
    using System.Runtime.InteropServices;

    [Serializable]
    public class ComReleaser : IDisposable
    {
        private ArrayList array = ArrayList.Synchronized(new ArrayList());

        ~ComReleaser()
        {
            this.Dispose(false);
        }

        public static void ReleaseCOMObject(object o)
        {
            if ((o != null) && Marshal.IsComObject(o))
            {
                while (Marshal.ReleaseComObject(o) > 0)
                {
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void ManageLifetime(object o)
        {
            this.array.Add(o);
        }

        protected virtual void Dispose(bool disposing)
        {
            int count = this.array.Count;
            for (int i = 0; i < count; i++)
            {
                if ((this.array[i] != null) && Marshal.IsComObject(this.array[i]))
                {
                    while (Marshal.ReleaseComObject(this.array[i]) > 0)
                    {
                    }
                }
            }

            if (disposing)
            {
                this.array = null;
            }
        }
    }
}
