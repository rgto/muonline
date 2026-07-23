using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    /// <summary>
    /// Âncora do marcador de clique-para-andar. O visual é 100% o efeito dourado
    /// réplica do MU mobile (ClickPinShineEffect: espeto girando + faíscas + anéis);
    /// o pin branco antigo (MoveTargetPosEffect.bmd) foi removido a pedido do usuário.
    /// </summary>
    public class CursorObject : WorldObject
    {
        private ClickCompassDecalEffect _compass;
        private ClickPinShineEffect _shine;

        public override async Task Load()
        {
            // Volume real: o culling do WorldControl usa frustum.Contains(BoundingBoxWorld);
            // caixa zerada faz o objeto sumir.
            BoundingBoxLocal = new BoundingBox(new Vector3(-80f, -80f, -20f),
                                               new Vector3(80f, 80f, 80f));
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            // World só existe depois que o cursor entra no mundo — por isso o efeito
            // é criado aqui (e no Show), não no Load().
            EnsureClickEffects();
            base.Update(gameTime);
        }

        protected override void OnPositionChanged()
        {
            base.OnPositionChanged();
            Show();
        }

        /// <summary>
        /// Reacende o marcador. Chamar SEMPRE que houver clique de movimento: o setter de
        /// Position só dispara OnPositionChanged quando o valor MUDA, então clicar de novo
        /// no mesmo tile (ou repetir a posição) deixava a marca invisível.
        /// </summary>
        public void Show()
        {
            // O cursor nasce em (0,0,0), FORA do grid espacial de culling do mundo —
            // o Update dele pode nunca ter rodado até o 1º clique. Garantir o efeito
            // aqui (World já existe: setado no Objects.Add do mundo).
            EnsureClickEffects();

            Hidden = false;
            Alpha = 1f;

            // Os efeitos vivem na RAIZ do mundo (não são filhos): entram no passe
            // transparente com Depth próprio e não herdam o alpha do cursor —
            // recebem a posição na mão.
            _compass?.PlayAt(Position);
            _shine?.PlayAt(Position);
        }

        /// <summary>
        /// Cria o efeito de clique (réplica do "dianji" do MU mobile) DIRETO em
        /// World.Objects: como raiz ele tem classificação de passe/Depth próprios
        /// (EffectObject é isento de culling e atualizado todo frame) e fica fora
        /// da cadeia TotalAlpha do cursor.
        /// </summary>
        private void EnsureClickEffects()
        {
            if (World == null)
                return;

            if (_compass == null)
            {
                _compass = new ClickCompassDecalEffect();
                World.Objects.Add(_compass);
            }

            if (_shine == null)
            {
                _shine = new ClickPinShineEffect();
                World.Objects.Add(_shine);
            }
        }

        public override void Dispose()
        {
            // Os efeitos são raiz do mundo: sem isso, recriar o cursor no MESMO mundo
            // deixaria órfãos acumulando em World.Objects.
            if (_compass != null)
            {
                World?.RemoveObject(_compass);
                _compass.Dispose();
                _compass = null;
            }
            if (_shine != null)
            {
                World?.RemoveObject(_shine);
                _shine.Dispose();
                _shine = null;
            }
            base.Dispose();
        }
    }
}
