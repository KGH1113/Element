#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <cstring>

// C ABI for the profile open and save windows. Strings are UTF-8. Unity calls
// managed code on the AppKit main thread; the panel runs modally there and
// returns once the player picks a file or cancels.
//
// Returns 1 with the chosen path in `output`, 0 when cancelled, -1 when the
// panel cannot run on this thread, and -2 when `output` is too small.

static NSString *Text(const char *value) {
  NSString *text = value ? [NSString stringWithUTF8String:value] : nil;
  return text ?: @"";
}

// `extensions` is one extension or a comma-separated list; open windows show
// every one, save windows save with the first.
static int Run(NSSavePanel *panel, const char *title, const char *extensions, char *output, int capacity) {
  NSMutableArray<UTType *> *types = [NSMutableArray array];
  for (NSString *type in [Text(extensions) componentsSeparatedByString:@","]) {
    if (type.length == 0) continue;
    UTType *contentType = [UTType typeWithFilenameExtension:type];
    if (contentType) [types addObject:contentType];
  }
  if (types.count > 0) panel.allowedContentTypes = types;
  // Panels have no visible title on current macOS; the message carries it.
  panel.title = Text(title);
  panel.message = Text(title);
  NSWindow *game = NSApp.keyWindow ?: NSApp.mainWindow;
  // A full-screen game window can sit above the normal panel level.
  if (game && game.level >= panel.level) panel.level = game.level + 1;
  NSModalResponse response = [panel runModal];
  [game makeKeyAndOrderFront:nil];
  if (response != NSModalResponseOK || !panel.URL) return 0;
  const char *path = panel.URL.fileSystemRepresentation;
  size_t length = path ? strlen(path) : 0;
  if (length == 0) return 0;
  if (length + 1 > (size_t)capacity) return -2;
  memcpy(output, path, length + 1);
  return 1;
}

extern "C" __attribute__((visibility("default"))) int element_file_dialog_open(
  const char *title, const char *extension, char *output, int capacity) {
  if (![NSThread isMainThread] || !output || capacity <= 0) return -1;
  @autoreleasepool {
    NSOpenPanel *panel = [NSOpenPanel openPanel];
    panel.canChooseFiles = YES;
    panel.canChooseDirectories = NO;
    panel.allowsMultipleSelection = NO;
    return Run(panel, title, extension, output, capacity);
  }
}

extern "C" __attribute__((visibility("default"))) int element_file_dialog_save(
  const char *title, const char *fileName, const char *extension, char *output, int capacity) {
  if (![NSThread isMainThread] || !output || capacity <= 0) return -1;
  @autoreleasepool {
    NSSavePanel *panel = [NSSavePanel savePanel];
    panel.canCreateDirectories = YES;
    // The panel adds the extension of an unregistered type itself; a name
    // that already ends with it would be saved as name.ext.ext.
    NSString *name = Text(fileName);
    NSString *type = Text(extension);
    if (type.length > 0 && [name.pathExtension caseInsensitiveCompare:type] == NSOrderedSame)
      name = name.stringByDeletingPathExtension;
    panel.nameFieldStringValue = name;
    return Run(panel, title, extension, output, capacity);
  }
}
