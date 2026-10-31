const fs = require('fs');
const path = require('path');
const vm = require('vm');
const crypto = require('crypto');
const {createCanvas, loadImage} = require('/Users/hyeongwoo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const root = path.resolve(__dirname, '..');
const source = process.argv[2] || path.join(root, 'Source/prototype.html');
const html = fs.readFileSync(source, 'utf8');
fs.copyFileSync(source, path.join(root, 'Source/prototype.html'));
const line = prefix => html.split('\n').find(s => s.startsWith(prefix));
const constants = [line('const TYPES='), line('const CHARACTERS='), line('const MONSTERS=')].join('\n');
const context = vm.createContext({});
vm.runInContext(constants + '\n' + line('function portrait(') + '\nthis.data={CHARACTERS,MONSTERS,TYPES,portrait};', context);
const {CHARACTERS, MONSTERS, TYPES, portrait} = context.data;
const character = line('for(const c of team)');
const charBody = character.slice(character.indexOf("ctx.fillStyle='#0006'"), character.indexOf("ctx.fillStyle='#3c4847'"));
const monster = line("if(e.type==='armored')ctx.scale");
const monsterBody = monster.slice(0, monster.indexOf('if(e.hp<e.maxhp)'));
const projectile = html.split('\n').find(s => s.startsWith('for(const b of shots)') && s.includes('ctx.save()'));
const projectileBody = projectile.slice(projectile.indexOf('ctx.rotate('), projectile.lastIndexOf('ctx.restore()'));
const deathBody = character.slice(character.indexOf("ctx.strokeStyle='#687675'"), character.indexOf('ctx.restore();continue'));
const names = ['bruno','hans','lena','yuki','bolt','mira'];
const assets=[];
const previews=[];
async function output(name, label, width, height, draw, category) {
  for (const scale of [1,4]) {
    const canvas=createCanvas(width*scale,height*scale), ctx=canvas.getContext('2d');
    ctx.scale(scale,scale);
    await draw(ctx);
    const pixels=ctx.getImageData(0,0,canvas.width,canvas.height).data;
    let opaque=0, transparent=0, edge=0;
    for(let y=0;y<canvas.height;y++) for(let x=0;x<canvas.width;x++) {
      const a=pixels[(y*canvas.width+x)*4+3];
      if(a) {opaque++; if(x===0||y===0||x===canvas.width-1||y===canvas.height-1)edge++;} else transparent++;
    }
    if(!opaque||!transparent||edge)throw Error(`Invalid bounds/alpha: ${name} ${scale}: ${opaque}/${transparent}/${edge}`);
    const relative=`${scale===4?'Sprites':'Reference1x'}/${name}.png`;
    fs.mkdirSync(path.dirname(path.join(root,relative)),{recursive:true});
    fs.writeFileSync(path.join(root,relative),canvas.toBuffer('image/png'));
    if(scale===4) {
      assets.push({file:relative,label,category,width:canvas.width,height:canvas.height,pivot:{x:.5,y:.5},pixelsPerUnit:168,sourcePixelsPerUnit:42,scale:4,alpha:true,edgeAlphaPixels:edge});
      previews.push({canvas,label:name.split('/').pop(),category});
    }
  }
}
async function main(){
  for(let i=0;i<CHARACTERS.length;i++) {
    const c=CHARACTERS[i];
    for(const flash of [false,true])await output(`Characters/${names[i]}${flash?'_hit':''}`,c.name,64,64,ctx=>{
      ctx.translate(32,32); new Function('ctx','c',charBody)(ctx,{...c,flash});
    },'character');
    const svg=portrait(c,i).replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" width="130" height="120" ');
    fs.mkdirSync(path.join(root,'Source/PortraitSVG'),{recursive:true});
    fs.writeFileSync(path.join(root,`Source/PortraitSVG/${names[i]}.svg`),svg);
    // Four-pixel transparent padding preserves the complete original SVG viewport.
    await output(`Portraits/${names[i]}`,c.name,138,128,async ctx=>{
      const scale=ctx.getTransform().a;
      const svgAtSize=svg.replace('width="130" height="120"',`width="${130*scale}" height="${120*scale}"`);
      const img=await loadImage(Buffer.from(svgAtSize.replace(/<text[\s\S]*?<\/text>/g,'')));ctx.drawImage(img,4,4,130,120);
      ctx.font='18px "'+(c.weapon==='knife'?'Apple Color Emoji':(['bow','firework'].includes(c.weapon)?'Zapf Dingbats':'Apple Symbols'))+'"';ctx.textAlign='center';ctx.fillStyle='#ddd';ctx.fillText(TYPES[c.weapon].icon,100,92);
    },'portrait');
  }
  for(const [type,m] of Object.entries(MONSTERS)) for(const flash of [false,true]) {
    await output(`Monsters/${type}${flash?'_hit':''}`,m.name,80,80,ctx=>{
      ctx.translate(40,40);new Function('ctx','e','MONSTERS',monsterBody)(ctx,{type,flash},MONSTERS);
    },'monster');
  }
  for(const type of ['knife','sling','club','bow','firework','pan','enemy'])await output(`Projectiles/${type}`,type,48,48,ctx=>{
    ctx.translate(24,24);new Function('ctx','b',projectileBody)(ctx,{type,dx:1,dy:0,travel:0});
  },'projectile');
  await output('Markers/dead','사망 표시',32,32,ctx=>{ctx.translate(16,16);new Function('ctx',deathBody)(ctx);},'marker');
  fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({sourceSha256:crypto.createHash('sha256').update(html).digest('hex'),method:'Original Canvas drawing statements and original SVG; no AI regeneration',unityWorldScale:'42 source pixels = 1 Unity unit',assets},null,2));
  const cols=6,cw=180,ch=160;
  const sheet=createCanvas(cols*cw,Math.ceil(previews.length/cols)*ch),ctx=sheet.getContext('2d');
  previews.forEach((a,i)=>{
    const x=i%cols*cw,y=Math.floor(i/cols)*ch;
    for(let yy=0;yy<ch;yy+=10)for(let xx=0;xx<cw;xx+=10){ctx.fillStyle=(xx/10+yy/10)%2?'#263238':'#1d282d';ctx.fillRect(x+xx,y+yy,10,10);}
    const s=Math.min(150/a.canvas.width,120/a.canvas.height);
    ctx.drawImage(a.canvas,x+(cw-a.canvas.width*s)/2,y+8+(120-a.canvas.height*s)/2,a.canvas.width*s,a.canvas.height*s);
    ctx.fillStyle='#edf3ed';ctx.font='12px sans-serif';ctx.textAlign='center';ctx.fillText(a.label,x+cw/2,y+148);
  });
  fs.writeFileSync(path.join(root,'Preview.png'),sheet.toBuffer('image/png'));
  console.log(JSON.stringify({sprites:assets.length,nativeReferences:assets.length,alphaAndBounds:'passed',output:root}));
}
main().catch(e=>{console.error(e);process.exit(1)});
