namespace PicStashly.Models.Responses;

public record StorageStats(
    int ImageCount,
    long TotalSizeBytes,
    int TagCount,
    int UntaggedImageCount,
    DateTime? OldestImage,
    DateTime? NewestImage);
