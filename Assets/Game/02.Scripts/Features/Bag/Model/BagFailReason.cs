// 가방 조작 결과(기획서 A5). None = 성공. 기획서의 4가지(Locked·OutOfBag·Overlap·CannotMerge) 밖은 기획서가 이유를 정하지 않은 실패다
public enum BagFailReason
{
    None,
    // 쓰러진 대원의 장비
    Locked,
    // 칸이 가방 밖
    OutOfBag,
    // 다른 아이템과 겹침
    Overlap,
    CannotMerge,
    // 보관함 아이템을 보관함으로 해제
    InStash,
    // 정비 화면이 아님
    NotPrep,
    // 없는 Uid(에러 로그)
    NotFound,
}
