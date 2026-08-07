# BCaT Architecture Validation

Generated: 2026-08-07 12:06 · mode: report

**0 error(s), 0 warning(s).** No error-severity rule is failing; the build gate passes.

## Summary by rule

| Rule | Severity | Failures | Title |
|---|---|---|---|
| BCAT-D003 | Error | PASS | Every XRSimpleInteractable resolves to an interaction target |
| BCAT-D004 | Error | PASS | Every XRSimpleInteractable is reachable by XRI casters |
| BCAT-D005 | Error | PASS | No missing script references |
| BCAT-D006 | Error | PASS | At most one AudioListener per platform branch |
| BCAT-L001 | Error | PASS | No Quest-only components outside Platform/Quest |
| BCAT-L002 | Error | PASS | No Desktop-only components outside Platform/Desktop |
| BCAT-L003 | Error | PASS | Platform/ contains only rigs and platform services |
| BCAT-L004 | Error | PASS | DevOnly subtrees are editor-only |
| BCAT-L005 | Error | PASS | Raw platform APIs used only in sanctioned files |
| BCAT-L006 | Error | PASS | World-interaction keyboard polling is centralized |
| BCAT-P001 | Error | PASS | Exactly one ScenePlatformBinding per inhabited scene |
| BCAT-P002 | Error | PASS | Platform branches are authored inactive |
| BCAT-P003 | Error | PASS | One root Platform group with Desktop/Quest children only |
| BCAT-P004 | Error | PASS | One EventSystem per scene with exactly one input module |
| BCAT-P005 | Error | PASS | One rig per kind, both under Platform/ |
| BCAT-P006 | Error | PASS | One XRInteractionManager, under Platform/Quest |
| BCAT-Q001 | Error | PASS | Trigger-only interaction targets carry an XR select surface |
| BCAT-Q002 | Warning | PASS | Both desktop and XR prompts are valid |
| BCAT-S001 | Error | PASS | Transition destination scenes are loadable |
| BCAT-S002 | Error | PASS | Transition spawn ids resolve |
| BCAT-S003 | Error | PASS | Each platform branch has a MainCamera |
| BCAT-S004 | Error | PASS | Presentation scenes are head-tracked on Quest |
| BCAT-S005 | Error | PASS | Quality tiers exist with the expected names |
| BCAT-S006 | Error | PASS | Black Kitchen Addressables group uses local paths |
| BCAT-S007 | Error | PASS | Android application identifier is project-owned |

