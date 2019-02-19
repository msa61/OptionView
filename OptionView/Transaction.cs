using System;
using System.Diagnostics;
using FileHelpers;

namespace OptionView
{
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]
    public class Transaction
    {
        [FieldConverter(typeof(CustomDate))]
        public DateTime Time;

        public string TransactionCode;
        public string TransactionSubcode;
        public string SecurityID;
        public string Symbol;
        public string BuySell;
        public string OpenClose;

        [FieldConverter(ConverterKind.Decimal, ".")] // The decimal separator is .
        public decimal? Quantity;

        [FieldConverter(typeof(CustomDate))]
        public DateTime? ExpireDate;

        [FieldConverter(ConverterKind.Decimal, ".")] // The decimal separator is .
        public decimal? Strike;
        public string InsType;
        [FieldConverter(ConverterKind.Decimal, ".")] // The decimal separator is .
        [FieldNullValue(typeof(Decimal), "0.0")]
        public decimal? Price;
        [FieldConverter(ConverterKind.Decimal, ".")] // The decimal separator is .
        [FieldNullValue(typeof(Decimal), "0.0")]
        public decimal? Fees;
        [FieldConverter(ConverterKind.Decimal, ".")] // The decimal separator is .
        public decimal? Amount;

        public string Description;
        public string AccountRef;


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare1;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare2;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare3;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare4;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare5;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare6;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string Spare7;
    }

    public class CustomDate : ConverterBase
    {
        public override object StringToField(string from)
        {
            return Convert.ToDateTime(from);
        }

    }

}


