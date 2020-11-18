using Metec.MVBDClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVBDAdapter.MVBD
{
    /// <summary>
    /// Allows the adapter to accept turning commands (pivot).
    /// </summary>
    internal interface IRotatable
    {
        /// <summary>
        /// Rotate the display to the right.
        /// </summary>
        /// <returns>The new position identifier.</returns>
        Metec.MVBDClient.Position TurnRight();

        /// <summary>
        /// Rotate the display to the left.
        /// </summary>
        /// <returns>The new position identifier.</returns>
        Metec.MVBDClient.Position TurnLeft();


        /// <summary>
        /// Reset the rotate of the display to default.
        /// </summary>
        /// <returns>The new position identifier.</returns>
        Metec.MVBDClient.Position ResetTurn();

        /// <summary>
        /// Rotate the display to a specialized position.
        /// </summary>
        /// <returns>The new position identifier.</returns>
        Metec.MVBDClient.Position SetPosition(Metec.MVBDClient.Position p);

        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <returns>The current used position.</returns>
        Metec.MVBDClient.Position GetPosition();
    }

    /// <summary>
    /// Abstract implementation for a adapter that can rotate the display output (pivot)
    /// </summary>
    /// <seealso cref="MVBDAdapter.MVBD.IRotatable" />
    abstract public class Rotatable_Base : IRotatable
    {
        /// <summary>
        /// Gets or sets the mapper for mapping MVBD data to a special type of device.
        /// </summary>
        /// <value>
        /// The MVBD data mapper.
        /// </value>
        internal MVBDAdapter.DeviceMapper.IMVBD_DeviceMapper Mapper { get; set; }


        /// <summary>
        /// Rotate the display to the right.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position TurnRight()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Mapper.DeviceInfo.WorkingPosition.Previous();
            }
            return Position.Front;
        }

        /// <summary>
        /// Rotate the display to the left.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position TurnLeft()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Mapper.DeviceInfo.WorkingPosition.Next();
            }
            return Position.Front;
        }

        /// <summary>
        /// Reset the rotate of the display to default.
        /// </summary>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        public virtual Metec.MVBDClient.Position ResetTurn()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = Position.Front;
            }
            return Position.Front;
        }

        /// <summary>
        /// Rotate the display to a specialized position.
        /// </summary>
        /// <param name="p"></param>
        /// <returns>
        /// The new position identifier.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Metec.MVBDClient.Position SetPosition(Metec.MVBDClient.Position p)
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition = p;
            }
            return Position.Front;
        }

        /// <summary>
        /// Gets the current position.
        /// </summary>
        /// <returns>
        /// The current used position.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Position GetPosition()
        {
            if (Mapper != null && Mapper.DeviceInfo != null)
            {
                return Mapper.DeviceInfo.WorkingPosition;
            }
            return Position.Front;
        }

    }


    /// <summary>
    /// Code by 'husayt' Mar 13 '09 at 
    /// https://stackoverflow.com/questions/642542/how-to-get-next-or-previous-enum-value-in-c-sharp
    /// </summary>
    public static class Extensions
    {

        /// <summary>
        /// Returns the next following element of an enum.
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="src">The source.</param>
        /// <returns>The next subsequent element of an enum element </returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static T Next<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argumnent {0} is not an Enum", typeof(T).FullName));

            T[] Arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) + 1;
            return (Arr.Length == j) ? Arr[0] : Arr[j];
        }

        /// <summary>
        /// Returns the previous element of an enum.
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="src">The source.</param>
        /// <returns>The previous element of an enum element </returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static T Previous<T>(this T src) where T : struct
        {
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argumnent {0} is not an Enum", typeof(T).FullName));

            T[] Arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) - 1;
            return (j<0) ? Arr[Arr.Length-1] : Arr[j];
        }
    }
}
