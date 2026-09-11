from PIL import Image

src = r'C:\project\mchs gsm\AVTOKarta\1788519811-Photoroom.ico'
out = r'C:\project\mchs gsm\AVTOKarta\app.ico'

img = Image.open(src).convert('RGBA')

sizes = [(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)]
images = []
for sz in sizes:
    resized = img.resize(sz, Image.LANCZOS)
    images.append(resized)

images[-1].save(out, format='ICO', sizes=sizes, append_images=images[:-1])
print('Saved')

# Verify
img2 = Image.open(out)
print('Sizes:', img2.info.get('sizes', 'none'))
