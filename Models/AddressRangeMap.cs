namespace RALogicEditor.Models
{
    public class AddressRangeMap
    {
        public string Region { get; set; } = "";
        public long SourceBase { get; set; }
        public long TargetBase { get; set; }
        public long Size { get; set; } // Bytes

        public bool IsInRange(long address)
        {
            return address >= SourceBase && address < SourceBase + Size;
        }

        public long CalculateTarget(long address)
        {
            long offset = address - SourceBase;
            return TargetBase + offset;
        }
    }
}