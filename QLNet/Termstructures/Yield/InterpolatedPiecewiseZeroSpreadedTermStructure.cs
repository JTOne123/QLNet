﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QLNet
{
   public class InterpolatedPiecewiseZeroSpreadedTermStructure<Interpolator> : ZeroYieldStructure
      where Interpolator : class, IInterpolationFactory, new()
   {
      public InterpolatedPiecewiseZeroSpreadedTermStructure(Handle<YieldTermStructure> h,
                                                            List<Handle<Quote>> spreads,
                                                            List<Date> dates,
                                                            Compounding compounding = Compounding.Continuous,
                                                            Frequency frequency = Frequency.NoFrequency,
                                                            DayCounter dc = default(DayCounter),
                                                            Interpolator factory = default(Interpolator))
      {
         originalCurve_ = h;
         spreads_ = spreads;
         dates_ = dates;
         times_ = new InitializedList<double>(dates.Count);
         spreadValues_ = new InitializedList<double>(dates.Count);
         compounding_ = compounding;
         frequency_ = frequency;
         dc_ = dc ?? new DayCounter();
         factory_ = factory ?? new Interpolator();
         Utils.QL_REQUIRE(!spreads_.empty(), () => "no spreads given");
         Utils.QL_REQUIRE(spreads_.Count == dates_.Count, () => "spread and date vector have different sizes");
         //registerWith(originalCurve_);
         originalCurve_.registerWith(update);
         for (int i = 0; i < spreads_.Count; i++)
            spreads_[i].registerWith(update);
         //registerWith(spreads_[i]);
         if (!originalCurve_.empty())
            updateInterpolation();
      }

      private Handle<YieldTermStructure> originalCurve_;
      private List<Handle<Quote>> spreads_;
      private List<Date> dates_;
      private List<double> times_;
      private List<double> spreadValues_;
      private Compounding compounding_;
      private Frequency frequency_;
      private DayCounter dc_;
      private Interpolator factory_;
      private Interpolation interpolator_;

      public override DayCounter dayCounter() { return originalCurve_.link.dayCounter(); }
      public override Calendar calendar() { return originalCurve_.link.calendar(); }

      public override int settlementDays() { return originalCurve_.link.settlementDays(); }

      public override Date referenceDate() { return originalCurve_.link.referenceDate(); }

      public override Date maxDate() { return originalCurve_.link.maxDate() < dates_.Last() ? originalCurve_.link.maxDate() : dates_.Last(); }

      protected override double zeroYieldImpl(double t)
      {
         double spread = calcSpread(t);
         InterestRate zeroRate = originalCurve_.link.zeroRate(t, compounding_, frequency_, true);
         InterestRate spreadedRate = new InterestRate(zeroRate.value() + spread,
                                                      zeroRate.dayCounter(),
                                                      zeroRate.compounding(),
                                                      zeroRate.frequency());
         return spreadedRate.equivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).value();
      }

      private double calcSpread(double t)
      {
         if (t <= times_.First())
            return spreads_.First().link.value();
         else if (t >= times_.Last())
            return spreads_.Last().link.value();
         else
            return interpolator_.value(t, true);
      }

      protected new void update()
      {
         if (!originalCurve_.empty())
         {
            updateInterpolation();
            base.update();
         }
         else
         {
            /* The implementation inherited from YieldTermStructure
               asks for our reference date, which we don't have since
               the original curve is still not set. Therefore, we skip
               over that and just call the base-class behavior. */
            base.update();
         }
      }

      private void updateInterpolation()
      {
         for (int i = 0; i < dates_.Count; i++)
         {
            times_[i] = timeFromReference(dates_[i]);
            spreadValues_[i] = spreads_[i].link.value();
         }
         interpolator_ = factory_.interpolate(times_, times_.Count, spreadValues_);
      }
   }

   public class PiecewiseZeroSpreadedTermStructure: InterpolatedPiecewiseZeroSpreadedTermStructure<Linear>
   {
      public PiecewiseZeroSpreadedTermStructure(Handle<YieldTermStructure> h,
                                                List<Handle<Quote>> spreads,
                                                List<Date> dates,
                                                Compounding compounding = Compounding.Continuous,
                                                Frequency frequency = Frequency.NoFrequency,
                                                DayCounter dc = default(DayCounter))
         : base(h, spreads, dates, compounding, frequency, dc, new Linear())
      { }
   }
}
