interface BrandMarkProps {
  variant?: 'auth' | 'sidebar' | 'compact';
}

export function BrandMark({ variant = 'compact' }: BrandMarkProps) {
  const auth = variant === 'auth';

  return (
    <div className={`brandMark brandMark--${variant}`}>
      <img
        src={auth ? '/brand/kryptonian-gateway.png' : '/brand/kryptonian-mark.png'}
        alt=""
        aria-hidden="true"
      />
      <div className="brandMarkText">
        <strong>Kryptonian Gateway</strong>
        <span>Medical device trust gateway</span>
      </div>
    </div>
  );
}
