ent-HandheldHealthAnalyzerUnpowered = health analyzer
    .desc = A hand-held body scanner capable of distinguishing vital signs of the subject.
ent-HandheldHealthAnalyzer = { ent-['HandheldHealthAnalyzerUnpowered', 'PowerCellSlotSmallItem'] }

  .suffix = Powered
  .desc = { ent-['HandheldHealthAnalyzerUnpowered', 'PowerCellSlotSmallItem'].desc }
ent-HandheldHealthAnalyzerEmpty = { ent-HandheldHealthAnalyzer }
    .suffix = Empty
    .desc = { ent-HandheldHealthAnalyzer.desc }
