function Y_lm = computeSphericalHarmonic(l, m, theta, phi)
    % Matches your game's theta/phi convention
    P_lm = legendre(l, cos(theta));
    if m >= 0
        P_lm = squeeze(P_lm(m+1, :, :));
    else
        P_lm = squeeze(P_lm(-m+1, :, :));
        P_lm = P_lm * (-1)^(-m) * factorial(l+m) / factorial(l-m);
    end
    Y_lm = P_lm .* exp(1i * m * phi);
    
    % Normalization
    norm_factor = sqrt((2*l+1) * factorial(l-m) / (4*pi * factorial(l+m)));
    Y_lm = norm_factor * Y_lm;
end