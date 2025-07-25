﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RawPrint.NetStd
{
    internal class SafePrinter : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafePrinter(IntPtr hPrinter)
            : base(true)  // Indicates that the handle should be released when the object is disposed
        {
            handle = hPrinter;
        }

        // Override the ReleaseHandle method to properly close the printer handle
        protected override bool ReleaseHandle()
        {
            if (IsInvalid)
            {
                return false;
            }

            var result = NativeMethods.ClosePrinter(handle) != 0;
            handle = IntPtr.Zero;

            return result;
        }

        // Starts a new document on the printer and returns the document ID
        public uint StartDocPrinter(DOC_INFO_1 di1)
        {
            var id = NativeMethods.StartDocPrinterW(handle, 1, ref di1);
            if (id == 0)
            {
                if (Marshal.GetLastWin32Error() == 1804)
                {
                    throw new Exception("The specified datatype is invalid, try setting 'Enable advanced printing features' in printer properties.", new Win32Exception());
                }
                throw new Win32Exception();
            }

            return id;
        }

        // Ends the document on the printer
        public void EndDocPrinter()
        {
            if (NativeMethods.EndDocPrinter(handle) == 0)
            {
                throw new Win32Exception();
            }
        }

        // Starts a new page on the printer
        public void StartPagePrinter()
        {
            if (NativeMethods.StartPagePrinter(handle) == 0)
            {
                throw new Win32Exception();
            }
        }

        // Ends the current page on the printer
        public void EndPagePrinter()
        {
            if (NativeMethods.EndPagePrinter(handle) == 0)
            {
                throw new Win32Exception();
            }
        }

        // Writes data to the printer
        public void WritePrinter(byte[] buffer, int size)
        {
            int written = 0;
            if (NativeMethods.WritePrinter(handle, buffer, size, ref written) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        // Retrieves the dependent files for the printer driver
        public IEnumerable<string> GetPrinterDriverDependentFiles()
        {
            int bufferSize = 0;

            // Retrieve the necessary buffer size
            // AB if (NativeMethods.GetPrinterDriver(handle, null, 3, IntPtr.Zero, 0, ref bufferSize) != 0 || Marshal.GetLastWin32Error() != 122) // 122 = ERROR_INSUFFICIENT_BUFFER
            if (NativeMethods.GetPrinterDriver(handle, string.Empty, 3, IntPtr.Zero, 0, ref bufferSize) != 0 || Marshal.GetLastWin32Error() != 122) // 122 = ERROR_INSUFFICIENT_BUFFER
            {
                throw new Win32Exception();
            }

            var ptr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                // Retrieve the printer driver information
                // AB if (NativeMethods.GetPrinterDriver(handle, null, 3, ptr, bufferSize, ref bufferSize) == 0)
                if (NativeMethods.GetPrinterDriver(handle, string.Empty, 3, ptr, bufferSize, ref bufferSize) == 0)
                {
                    throw new Win32Exception();
                }

                // AB                 var di3 = (DRIVER_INFO_3)Marshal.PtrToStructure(ptr, typeof(DRIVER_INFO_3));
                var di3 = Marshal.PtrToStructure<DRIVER_INFO_3>(ptr);

                return ReadMultiSz(di3.pDependentFiles).ToList(); // Convert dependent files to a list
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // Helper method to read a multi-string from a pointer
        private static IEnumerable<string> ReadMultiSz(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                yield break;
            }

            var builder = new StringBuilder();
            var pos = ptr;

            while (true)
            {
                var c = (char)Marshal.ReadInt16(pos);

                if (c == '\0')
                {
                    if (builder.Length == 0)
                    {
                        break;
                    }

                    yield return builder.ToString();
                    builder = new StringBuilder();
                }
                else
                {
                    builder.Append(c);
                }

                pos += 2;
            }
        }

        // Opens the printer and returns a SafePrinter instance
        public static SafePrinter OpenPrinter(string printerName, ref PRINTER_DEFAULTS defaults)
        {
            IntPtr hPrinter;

            if (NativeMethods.OpenPrinterW(printerName, out hPrinter, ref defaults) == 0)
            {
                throw new Win32Exception();
            }

            return new SafePrinter(hPrinter);
        }
    }
}
