using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.DeviceMapper
{
    internal interface IMVBD_DeviceMapper
    {

        /// <summary>
        /// Converts the MVBD key code to a BrailleIO button.
        /// </summary>
        /// <param name="key">The MVBD key code.</param>
        /// <param name="general">The general button or NONE.</param>
        /// <param name="keyboard">The keyboard button or NONE.</param>
        /// <param name="additional">The array of additional keys or NULL.</param>
        /// <param name="genericButtonId">The generic button identifier if it does not fit into the general button identifiers.</param>
        /// <returns>
        ///   <c>true</c> if the key could be translated; otherwise, <c>false</c>.
        /// </returns>
        bool ConvertToBioButton(int key, 
            out BrailleIO.Interface.BrailleIO_DeviceButton general, 
            out BrailleIO.Interface.BrailleIO_BrailleKeyboardButton keyboard, 
            out BrailleIO.Interface.BrailleIO_AdditionalButton[] additional,
            out String genericButtonId);

        /// <summary>
        /// Builds the device data struct for an MVBD wrapped device.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The device identifying struct</returns>
        BrailleIO.BrailleIODevice BuildDevice(Metec.MVBDClient.MVBDDeviceInfo args);

        /// <summary>
        /// Gets the device Object for this wrapped pin device.
        /// Call <see cref="BuildDevice"/> first. 
        /// </summary>
        /// <value>
        /// The device property set.
        /// </value>
        BrailleIO.BrailleIODevice Device { get; }

        /// <summary>
        /// Gets the device information.
        /// Call <see cref="BuildDevice"/> first. 
        /// </summary>
        /// <value>
        /// The device information.
        /// </value>
        Metec.MVBDClient.MVBDDeviceInfo DeviceInfo { get; }

        /// <summary>
        /// Converts a finger to a touch.
        /// </summary>
        /// <param name="finger">The finger to convert.</param>
        /// <returns>The Touch definition for this finger.</returns>
        BrailleIO.Structs.Touch ConvertFingerToTouch(Metec.MVBDClient.MVBDFinger finger);
    }
}
