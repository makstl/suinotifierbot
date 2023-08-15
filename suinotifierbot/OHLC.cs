public struct OHLC
{
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public decimal Volume;
    public DateTime TimeStamp;
    public (decimal Min, decimal Max) Body => (Math.Min(Open, Close), Math.Max(Open, Close));
}
