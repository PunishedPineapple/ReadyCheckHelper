namespace ReadyCheckHelper;
public enum ReadyCheckState : byte
{
	Unknown = 0,
	AwaitingResponse = 1,
	Ready = 2,
	NotReady = 3,
	CrossWorldMemberNotPresent = 4
}
